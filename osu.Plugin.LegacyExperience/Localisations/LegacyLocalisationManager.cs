using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Extensions;
using LazerLanguage = osu.Game.Localisation.Language;

namespace osu.Plugin.LegacyExperience.Localisations;

public partial class LegacyLocalisationManager : Component
{
    public const string RESOURCE_PREFIX = "osu.Plugin.LegacyExperience";

    private Storage localisationStorage = null!;

    [Resolved]
    private OsuGameBase game { get; set; } = null!;

    [Resolved]
    private LocalisationManager localisations { get; set; } = null!;

    public IBindable<LegacyLanguageCodes> CurrentLegacyLanguage => currentLegacyLanguage;

    private HttpClient httpClient = null!;

    private readonly IBindable<LazerLanguage> currentLazerLanguage = new Bindable<LazerLanguage>();
    private readonly Bindable<LegacyLanguageCodes> currentLegacyLanguage = new Bindable<LegacyLanguageCodes>();

    private Bindable<string> frameworkLocale = null!;

    [BackgroundDependencyLoader]
    private void load(Storage storage, FrameworkConfigManager config)
    {
        httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        frameworkLocale = config.GetBindable<string>(FrameworkSetting.Locale);

        // osu framework requires localisation stores to be there beforehand.
        var stores = new Dictionary<string, LocalisationStore>();

        foreach (var lang in Enum.GetValues<LazerLanguage>())
        {
            var cultureCode = lang.ToCultureCode();

            if (stores.ContainsKey(cultureCode) || cultureCode == "debug")
                continue;

            var store = new LocalisationStore(lang.ToLegacy());

            stores.Add(cultureCode, store);

            // no osu instance handling localisation, add our store to the framework's localisation manager directly.
            if (!tryAddToOsuResourceManager(cultureCode, store))
                localisations.AddLanguage(cultureCode, store);
        }

        this.stores = stores.ToFrozenDictionary();

        localisationStorage = createLocalisationStorage(storage);

        currentLazerLanguage.BindTo(game.CurrentLanguage);
        currentLazerLanguage.BindValueChanged(v => currentLegacyLanguage.Value = v.NewValue.ToLegacy(), true);
        currentLegacyLanguage.BindValueChanged(v => updateLocalisation(v.NewValue), true);
    }

    private FrozenDictionary<string, LocalisationStore> stores = null!;

    private CancellationTokenSource? localisationLoadCancellation;
    private void updateLocalisation(LegacyLanguageCodes lang)
    {
        if (loadedStores.ContainsKey(lang))
            return;

        if (!stores.TryGetValue(lang.ToCultureCode(), out var store))
            return;

        localisationLoadCancellation?.Cancel();
        localisationLoadCancellation?.Dispose();
        localisationLoadCancellation = new CancellationTokenSource();

        _ = Task.Run(async () => await loadLocalisations(lang, store, localisationLoadCancellation.Token), localisationLoadCancellation.Token);
    }

    private readonly ConcurrentDictionary<LegacyLanguageCodes, LocalisationStore> loadedStores = new();

    private async Task loadLocalisations(LegacyLanguageCodes lang, LocalisationStore store, CancellationToken cancellationToken = default)
    {
        var rawLocalisation = await loadRawLocalisation(lang, cancellationToken);

        if (rawLocalisation is null)
        {
            fail("Could not download localisation data.");
            return;
        }

        try
        {
            store.AssignLocalisationData(rawLocalisation);
            loadedStores[lang] = store;

            // Trigger the framework to reload localisation data
            Scheduler.Add(() =>
            {
                var locale = frameworkLocale.Value;
                frameworkLocale.Value = string.Empty;
                frameworkLocale.Value = locale;
            });

            Logger.Log($"Localisation data for {lang} loaded successfully!", LoggingTarget.Information, LogLevel.Verbose);
        }
        catch (Exception e)
        {
            fail(e.Message);
            return;
        }

        void fail(string? message = null)
        {
            Logger.Log($"Failed to load localisation for {lang}, falling back to English. {message}", LoggingTarget.Information, LogLevel.Important);
        }
    }

    private const string stable_resource_base_url = "https://m1.ppy.sh/release/Localisation/";

    private async Task<string?> loadRawLocalisation(LegacyLanguageCodes lang, CancellationToken cancellationToken = default)
    {
        string filename = Path.ChangeExtension(lang.ToLegacyCode(), ".txt");

        if (!localisationStorage.Exists(filename))
        {
            Logger.Log($"Downloading localisation data for {lang}...", LoggingTarget.Information, LogLevel.Important);

            var url = new Uri($"{stable_resource_base_url}{filename}?{DateTime.Today.ToFileTimeUtc()}");

            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // if the request fails for other reasons (e.g. network issues), we just keep the current localisation (which may be outdated) and try again next time.
                    return null;
                }

                Logger.Log($"Successfully downloaded osu!stable localisation for {lang} ({filename})", LoggingTarget.Runtime, LogLevel.Verbose);

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                using (var fs = localisationStorage.CreateFileSafely(filename))
                    // don't apply cancellationToken, or the file may end up being created but with incomplete data
                    await fs.WriteAsync(bytes);

                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception e)
            {
                Logger.Log($"Could not download osu!stable localisation data: {e.Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                return null;
            }
        }

        Logger.Log($"Loading localisation for {lang} ({filename}) from local storage", LoggingTarget.Runtime, LogLevel.Verbose);

        using (var fs = localisationStorage.GetStream(filename))
        using (var sr = new StreamReader(fs))
            return await sr.ReadToEndAsync(cancellationToken);
    }

    private const string localisation_folder = "Localisation"; // matches osu!stable's localisation folder name.

    private Storage createLocalisationStorage(Storage storage)
    {
        bool newlyCreated = !storage.ExistsDirectory(localisation_folder);
        var localisationStorage = storage.GetStorageForDirectory(localisation_folder);

        if (newlyCreated)
        {
            using (var fs = localisationStorage.CreateFileSafely("IMPORTANT README.txt"))
            using (var sw = new StreamWriter(fs))
            {
                sw.WriteLine("This folder is used to store localisations for the legacy experience plugin.");
                sw.WriteLine("All files in this folder are automatically downloaded from osu!stable server.");
            }
        }

        return localisationStorage;
    }

    private partial class LocalisationStore : ILocalisationStore
    {
        public CultureInfo EffectiveCulture { get; }
        public bool IsAvailable => localisations is not null;

        private FrozenDictionary<string, string>? localisations = null;

        public LocalisationStore(LegacyLanguageCodes langCode)
        {
            EffectiveCulture = langCode.GetEffectiveCultureInfo();
        }

        public void AssignLocalisationData(string rawLocalisation)
        {
            var localisations = new Dictionary<string, string>();

            foreach (var line in rawLocalisation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var splitIndex = line.IndexOf('=');
                if (splitIndex <= 0)
                    continue;

                var key = line[..splitIndex];
                var value = line[(splitIndex + 1)..];

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                    continue;

                // avoid conflicts with any existing resources in the game by prefixing all keys with a unique string.
                key = GetKey(key);

                localisations[key] = value;
            }

            this.localisations = localisations.ToFrozenDictionary();
        }

#nullable disable // ILocalisationStore and IResourceStore expect nullability to be disabled

        public string Get(string name) => localisations?.TryGetValue(name, out var value) ?? false ? value : null!;

        public Task<string> GetAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Get(name));

        public IEnumerable<string> GetAvailableResources() => localisations?.Keys ?? Enumerable.Empty<string>();

        public Stream GetStream(string name)
        {
            if (Get(name) is string value)
                return new MemoryStream(Encoding.UTF8.GetBytes(value));

            return null!;
        }

        public void Dispose()
        {
        }

#nullable enable
    }

    public static string GetKey(string key) => $"{RESOURCE_PREFIX}:{key}";

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        // user may create this object and dispose it without adding it to the scene graph,
        // so null check here just to be safe.
        httpClient?.Dispose();
        localisationLoadCancellation?.Cancel();
        localisationLoadCancellation?.Dispose();
    }
}
