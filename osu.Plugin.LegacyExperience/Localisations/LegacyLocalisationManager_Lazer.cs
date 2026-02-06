using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
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
        frameworkLocales ??= (Dictionary<string, LocaleMapping>)locales_Field.GetValue(localisations)!;

        Debug.Assert(frameworkLocales is not null);

        if (!frameworkLocales.TryGetValue(cultureCode, out var mapping))
            return false;

        if (mapping.Storage is not ResourceManagerLocalisationStore resourceManagerStore)
        {
            Logger.Log($"Legacy localisation will not work for culture {cultureCode}, see log for details.", level: LogLevel.Error);
            Logger.Log($"Expected {nameof(ResourceManagerLocalisationStore)}, got {mapping.Storage.GetType().FullName}", level: LogLevel.Error);

            // don't allow AddLanguage call.
            return true;
        }

        var resourceManagers = (Dictionary<string, ResourceManager>)resourceManagers_Field.GetValue(resourceManagerStore)!;

        Debug.Assert(resourceManagers is not null);

        lock (resourceManagers)
        {
            if (resourceManagers.ContainsKey(RESOURCE_PREFIX))
                return true;

            resourceManagers[RESOURCE_PREFIX] = new LocalisationResourceManager(store);
        }

        return true;
    }

    private class LocalisationResourceManager : ResourceManager
    {
        private readonly LocalisationStore store;

        public LocalisationResourceManager(LocalisationStore store)
        {
            this.store = store;
        }

        public override string? GetString(string name) => store.Get(GetKey(name));

        public override string? GetString(string key, CultureInfo? culture = null) => GetString(key);
    }
}
