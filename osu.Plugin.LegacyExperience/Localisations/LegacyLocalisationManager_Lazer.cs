using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Localisation;

namespace osu.Plugin.LegacyExperience.Localisations;

partial class LegacyLocalisationManager
{
    private static readonly FieldInfo locales_Field = typeof(LocalisationManager)
        .GetField("locales", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo resourceManagers_Field = typeof(ResourceManagerLocalisationStore)
        .GetField("resourceManagers", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private Dictionary<string, LocaleMapping> frameworkLocales = null!;

    // osu!lazer handles localisation through ResourceManager instances and theres no way to add new ones at runtime, 
    // so we have to inject our own ResourceManager into the existing osu!lazer system.
    private bool tryAddToOsuResourceManager(string cultureCode, LocalisationStore store)
    {
        if (locales_Field is null || resourceManagers_Field is null)
        {
            Logger.Log("legacy localisation will not work. See log for details.", LoggingTarget.Runtime, LogLevel.Error);
            Logger.Log($"Could not find required fields via reflection. {nameof(locales_Field)}: {locales_Field != null}, {nameof(resourceManagers_Field)}: {resourceManagers_Field != null}", LoggingTarget.Runtime, LogLevel.Verbose);

            return true;
        }

        frameworkLocales ??= (Dictionary<string, LocaleMapping>)locales_Field.GetValue(localisations)!;

        Debug.Assert(frameworkLocales is not null);

        if (!frameworkLocales.TryGetValue(cultureCode, out var mapping))
            return false;

        if (mapping.Storage is not ResourceManagerLocalisationStore resourceManagerStore)
        {
            Logger.Log($"Legacy localisation will not work for culture {cultureCode}, see log for details.", LoggingTarget.Runtime, LogLevel.Error);
            Logger.Log($"Expected {nameof(ResourceManagerLocalisationStore)}, got {mapping.Storage.GetType().FullName}", LoggingTarget.Runtime, LogLevel.Verbose);

            // don't allow AddLanguage call.
            return true;
        }

        var resourceManagers = (Dictionary<string, ResourceManager>)resourceManagers_Field.GetValue(resourceManagerStore)!;

        Debug.Assert(resourceManagers is not null);

        lock (resourceManagers)
        {
            if (resourceManagers.ContainsKey(RESOURCE_PREFIX))
                return true;

            resourceManagers[RESOURCE_PREFIX] = createResourceManager(store);
        }

        return true;
    }

    private static LocalisationResourceManager createResourceManager(LocalisationStore store)
    {
        // We are oveserving System.MissingMethodException: "Method not found: 'Void System.Resources.ResourceManager..ctor()'" on some platforms (Android).
        // Using RuntimeHelpers.GetUninitializedObject as a workaround to create an instance without calling the constructor.
        // Since only GetString is expected to be used, this should be safe in our case.
        var manager = (LocalisationResourceManager)RuntimeHelpers.GetUninitializedObject(typeof(LocalisationResourceManager));
        manager.Store = store;
        return manager;
    }

    private class LocalisationResourceManager : ResourceManager
    {
        internal required LocalisationStore Store { get; set; }

        public override string? GetString(string name) => Store.Get(GetKey(name));

        public override string? GetString(string key, CultureInfo? culture = null) => GetString(key);
    }
}
