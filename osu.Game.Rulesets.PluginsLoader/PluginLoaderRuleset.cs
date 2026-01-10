using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Plugins;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.PluginsLoader;

public partial class PluginLoaderRuleset : Ruleset
{
    static PluginLoaderRuleset()
    {
        // Manual load is required for:
        // - Skin extension plugins requires their types to be loaded very early or the game throws exceptions as type resolution fails.
        // - AOT platforms(iOS/Android) where assemblies are not automatically loaded when referenced.
        loadPluginAssembly();
    }

    // Our code rarely caches private members because we expect access them is fast with UnsafeAccessor, this is designed.
    // However, Some platforms do not support UnsafeAccessor, especially iOS and Android.
    // I've tried on my own device and it either:
    // - throws TypeLoadException when accessing UnsafeAccessorAttribute, this is probably due to trimming.
    // - mono CLR crashes(native code) immediately when it tries to generate accessor method(I build the game in debug mode and disabled trimming).
    // Seems like UnsafeAccessor won't be able to work on those platforms in the near future.
    // Although fallback to reflection is possible, it is not worth the maintenance cost.
    // Also, we can't assume how frequently the plugins call those accessors, so performance impact is uncertain.
    // so there's no plan of supporting those platforms in the near future because few users are expected to run osu! on those platforms.
    internal static bool IsUnsupportedPlatforms => RuntimeInfo.OS switch
    {
        RuntimeInfo.Platform.Android or
        RuntimeInfo.Platform.iOS => true,
        _ => false,
    };

    private static void loadPluginAssembly()
    {
        const string searchPattern = "osu.Game.Plugins*.dll";

        static void loadSingle(string assemblyFile)
        {
            try
            {
                // Attempt to load the assembly by name.
                Assembly.LoadFrom(assemblyFile);
            }
            catch
            {
                // verbose is enough, as our code won't work anymore.
                Logger.Log($"Failed to load plugin assembly: {assemblyFile}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
        }

        var ourDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (string.IsNullOrEmpty(ourDirectory))
            return;

        // We assume required assemblies are next to us, as they are usually published that way.
        var dlls = Directory.GetFiles(ourDirectory, searchPattern, SearchOption.TopDirectoryOnly);

        foreach (var file in dlls)
            loadSingle(file);
    }

    public PluginLoaderRuleset(object? _)
    {
        // Dummy constructor to differentiate instantiation from RulesetStore
    }

    /// <summary>
    /// This constructor is intended to be called by <see cref="RulesetStore"/> via reflection only.
    /// It is very slow (usually sub-millisecond, and up to a few milliseconds when injecting into the game),
    /// Use <see cref="PluginLoaderRuleset(object?)"/> if possible.
    /// </summary>
    [Obsolete("This constructor is intended to be called by RulesetStore via reflection only.", true)]
    public PluginLoaderRuleset()
    {
        try
        {
            var game = RetrieveCurrentOsuGame();

            if (game is null)
                return;

            lock (processed_games)
            {
                // avoid double-processing the same game instance.
                if (!processed_games.Contains(game) && RegisterProcessedGame(game))
                    processed_games.Add(game);
                else
                    return;
            }

            // Skin plugins requires their types loaded at a quite early stage.
            // We have to ensure the injection is performed as soon as possible, and thus we have to block the constructor.
            PerformStaticGameInjection(game);
        }
        // We have to be very defensive here, as the game has no protection against ruleset constructor failures.
        // Any exception thrown here would crash the entire game.
        catch
        {
        }
    }

    private static void PerformStaticGameInjection(OsuGame game)
    {
        try
        {
            game.PerformPluginsLoad();
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to perform static game injection: {ex.Message}. Plugins manager will not function.", LoggingTarget.Runtime, LogLevel.Error);
        }
    }

    private static readonly HashSet<OsuGame> processed_games = new();

    private const BindingFlags internal_binding_flags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly MethodInfo drawable_disposed_event = typeof(Drawable)
        .GetEvent("OnDispose", internal_binding_flags)?
        .AddMethod!;

    private static bool RegisterProcessedGame(OsuGame game)
    {
        Debug.Assert(!processed_games.Contains(game));
        Debug.Assert(drawable_disposed_event is not null);

        drawable_disposed_event.Invoke(game, new[] { () =>
        {
            lock (processed_games)
            {
                processed_games.Remove(game);
            }
        } });

        return true;
    }

    private static MethodInfo load_thread_getter = typeof(Drawable)
        .GetProperty("LoadThread", internal_binding_flags)?
        .GetMethod!;

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "NewEntry")]
    private extern static ref Action<LogEntry> get_log_entry_delegate(Logger _);

    private OsuGame? RetrieveCurrentOsuGame()
    {
        Debug.Assert(load_thread_getter is not null);
        Thread? current_thread = null;

        bool IsUnprocessedGame(OsuGame game)
        {
            if (game.LoadState is not LoadState.Loading)
                return false;

            if (load_thread_getter.Invoke(game, null) is not Thread loadThread ||
                loadThread != (current_thread ??= Thread.CurrentThread))
                return false;

            return true;
        }

        return PluginHelper.GetGameStatically()
                .OfType<OsuGame>()
                // I don't think the race condition matters here, as those are not our candidates.
                // The candidates must be executing on the current thread, so real candidates won't be missed.
                // Same as above when we access load thread.
                .Where(g => !processed_games.Contains(g))
                .Distinct()
                // We've now ensured only one candidate can match, so we can safely use SingleOrDefault.
                .SingleOrDefault(IsUnprocessedGame);
    }

    public override string ShortName => "Plugins";
    public override string Description => "Provide plugin functionality";
    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new DummyBeatmapConverter(beatmap, this);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new DummyDifficultyCalculator(RulesetInfo, beatmap);

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => throw new NotImplementedException("This ruleset is not meant to be played.");

    public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

    public override Drawable CreateIcon() => new SpriteIcon
    {
        RelativeSizeAxes = Axes.Both,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Icon = FontAwesome.Solid.PuzzlePiece
    };

    private class DummyDifficultyCalculator : DifficultyCalculator
    {
        public DummyDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
            => new DifficultyAttributes(mods, 0);

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
            => Array.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
            => Array.Empty<Skill>();
    }

    private class DummyBeatmapConverter : BeatmapConverter<HitObject>
    {
        public DummyBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => true;
    }
}
