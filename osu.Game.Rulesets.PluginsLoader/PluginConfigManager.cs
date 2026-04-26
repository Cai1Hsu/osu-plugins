using System.Diagnostics;
using System.Reflection;
using AccessItEasy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Plugins;

namespace osu.Game.Rulesets.PluginsLoader;

public partial class PluginConfigManager : ConfigManager
{
    public const string ConfigFile = "plugin_config.json";

    private readonly Storage storage;

    private readonly Dictionary<string, Dictionary<string, IBindable>> pluginSettings;

    public PluginConfigManager(Storage storage, OsuPlugin[] plugins)
    {
        this.storage = storage;

        pluginSettings = plugins.ToDictionary(getUniqueName, createSettings);

        Load();

        bindSaveOperations();
    }

    private string getUniqueName(OsuPlugin plugin)
    {
        var type = plugin.GetType();

        var fullyQualifiedName = type.AssemblyQualifiedName;

        Debug.Assert(fullyQualifiedName is not null);

        // we don't want version and public key token to be part of the unique name as they are not relevant to identifying a plugin and would cause unnecessary config breakage on new builds.
        var nameParts = fullyQualifiedName.Split(',').Select(p => p.Trim()).Take(2);

        return string.Join(',', nameParts);
    }

    private Dictionary<string, IBindable> createSettings(OsuPlugin plugin)
    {
        Debug.Assert(plugin is not null);

        var enabled = (IBindable)plugin.Enabled;

        var settings = plugin.GetOrderedSettingsSourceProperties()
            .Where(p => p.Item2.PropertyType.EnumerateBaseTypes().Any(
                t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Bindable<>)))
            .Select(p => (p.Item1.Label.ToString(), (p.Item2.GetValue(plugin) as IBindable)!))
            .Where(b => b.Item2 is not null);

        return (nameof(OsuPlugin.Enabled), enabled).Yield()
                                                   .Concat(settings)
                                                   .ToDictionary();
    }

    private void bindSaveOperations()
    {
        var subscribeMethod = typeof(PluginConfigManager).GetMethod(nameof(subscribe), BindingFlags.NonPublic | BindingFlags.Instance);

        Debug.Assert(subscribeMethod is not null);

        var instantiateSubscribeMethods = new Dictionary<Type, MethodInfo>();

        foreach (var (_, settings) in pluginSettings)
        {
            foreach (var (_, bindable) in settings)
            {
                var propType = bindable.GetType();

                if (instantiateSubscribeMethods.TryGetValue(propType, out var method))
                {
                    method.Invoke(this, new object[] { bindable });
                    continue;
                }

                var bindableType = propType.EnumerateBaseTypes()
                    // find implemented IBindable<T>
                    .SelectMany(t => t.GetInterfaces())
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBindable<>));

                if (bindableType is null)
                {
                    Logger.Log($"Bindable property {propType.ReadableName()} does not implement IBindable<T> and will be ignored for change tracking.",
                        LoggingTarget.Runtime, DebugUtils.IsDebugBuild ? LogLevel.Error : LogLevel.Verbose);
                    continue;
                }

                var encapsulatedType = bindableType.GetGenericArguments()[0];

                if (!instantiateSubscribeMethods.TryGetValue(encapsulatedType, out method))
                {
                    method = subscribeMethod.MakeGenericMethod(encapsulatedType);
                    instantiateSubscribeMethods[encapsulatedType] = method;
                    instantiateSubscribeMethods[propType] = method;
                }

                method.Invoke(this, new object[] { bindable });
            }
        }
    }

    private void subscribe<T>(IBindable<T> b) => b.ValueChanged += _ => QueueBackgroundSave();

    protected override void PerformLoad()
    {
        try
        {
            using var fs = storage.GetStream(ConfigFile, FileAccess.Read, FileMode.OpenOrCreate);

            if (fs is null)
                return;

            using (var sr = new StreamReader(fs))
            {
                var json = sr.ReadToEnd();

                if (string.IsNullOrWhiteSpace(json))
                    return;

                // somehow PopulateObject overwrites existing bindable instances even if set ObjectCreationHandling to Reuse
                var root = JObject.Parse(json);
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                    NullValueHandling = NullValueHandling.Ignore,
                });

                foreach (var pluginProperty in root.Properties())
                {
                    if (pluginProperty.Value is not JObject settingsObject)
                        continue;

                    if (!pluginSettings.TryGetValue(pluginProperty.Name, out var settings))
                        continue;

                    foreach (var settingProperty in settingsObject.Properties())
                    {
                        if (!settings.TryGetValue(settingProperty.Name, out var bindable))
                            continue;

                        if (settingProperty.Value.Type == JTokenType.Null)
                            continue;

                        using var tokenReader = settingProperty.Value.CreateReader();
                        BindableConverter.DeserializeFrom(bindable, tokenReader, serializer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load plugin configuration.");
        }
    }

    protected override bool PerformSave()
    {
        try
        {
            using (var fs = storage.CreateFileSafely(ConfigFile))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(JsonConvert.SerializeObject(pluginSettings, Formatting.Indented, new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { new BindableConverter() }
                }));
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save plugin configuration.");
            return false;
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Save();
    }

#nullable disable

    private partial class BindableConverter : JsonConverter<IBindable>
    {
        [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(DeserializeFrom))]
        internal extern static void DeserializeFrom(
            [PrivateAccessorType("osu.Framework.IO.Serialization.ISerializableBindable")] object instance, JsonReader reader, JsonSerializer serializer);

        [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(SerializeTo))]
        private extern static void SerializeTo(
            [PrivateAccessorType("osu.Framework.IO.Serialization.ISerializableBindable")] object instance, JsonWriter writer, JsonSerializer serializer);

        public override IBindable ReadJson(JsonReader reader, Type objectType, IBindable existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (existingValue is null)
            {
                var obj = serializer.Deserialize(reader);
                var bindableType = typeof(Bindable<>).MakeGenericType(obj.GetType());

                return (IBindable)Activator.CreateInstance(bindableType, obj);
            }

            DeserializeFrom(existingValue, reader, serializer);
            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, IBindable value, JsonSerializer serializer)
        {
            SerializeTo(value, writer, serializer);
        }
    }
#nullable restore
}
