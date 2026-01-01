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

public class PluginLoaderRuleset : Ruleset
{
    static PluginLoaderRuleset()
    {
        // In certain platforms (notably iOS and Android), referenced assemblies are not automatically loaded.
        // As such, we manually load any assemblies matching our plugin pattern to ensure they are available
        // This action has to be performed before any attempt to access types from those assemblies or our code crashes.
        if (requiresDynamicAssemblyLoading())
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

    private static bool requiresDynamicAssemblyLoading()
        // it happen to be the platforms that don't support UnsafeAccessor.
        // although our code won't actually work on those platforms,
        // let's still run a bit to at least notify the user about the lack of support.
        => IsUnsupportedPlatforms;

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

            // avoid double-processing the same game instance.
            if (IsGameProcessed(game))
                return;

            // This method is REALLY SLOW, so we run it later, after we ensured the ruleset is fully constructed.
            if (!IsInstantiatedFromRulesetStore())
                return;

            lock (processed_games)
                processed_games.Add(new WeakReference<OsuGame>(game));

            Task.Run(() => PerformStaticGameInjection(game));
        }
        // We have to be very defensive here, as the game has no protection against ruleset constructor failures.
        // Any exception thrown here would crash the entire game.
        catch
        {
        }
    }

    private static void PerformStaticGameInjection(OsuGame game)
    {
        static void logFailure(Exception ex)
        {
            Logger.Log($"Failed to perform static game injection: {ex.Message}. Plugins manager will not function.", LoggingTarget.Runtime, LogLevel.Error);
        }

        try
        {
            game.InvokeWhenReady(d =>
            {
                try
                {
                    var game = (OsuGame)d;
                    game.InjectDependencies<PluginManager>(out var _, () => new());
                }
                catch (Exception ex)
                {
                    logFailure(ex);
                }
            }, true);
        }
        catch (Exception ex)
        {
            logFailure(ex);
        }
    }

    private readonly static List<WeakReference<OsuGame>> processed_games = new();
    private static bool IsGameProcessed(OsuGame game)
    {
        lock (processed_games)
        {
            return processed_games.Any(wr => wr.TryGetTarget(out var target) && target == game);
        }
    }

    private static FieldInfo? logger_new_entry_field = null;
    private static OsuGame? RetrieveCurrentOsuGame()
    {
        [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "NewEntry")]
        static extern ref Action<LogEntry> GetLoggerNewEntryEventHandler(Logger _);

        static Action<LogEntry>? tryRetrieveLoggerHandler(Func<Action<LogEntry>?> retrievalMethod)
        {
            try
            {
                return retrievalMethod();
            }
            catch
            {
                return null;
            }
        }

        // SAFETY:
        // This is our primary way of initially getting the current OsuGame instance,
        // it has some unsafe assumptions about the game & launcher's implementation.
        // Like, the invocation order only reflects the creation order of OsuGame instances.
        return (tryRetrieveLoggerHandler(() => GetLoggerNewEntryEventHandler(null!)) ??
               tryRetrieveLoggerHandler(() =>
               {
                   // Reflection is slow, but at least faster than scanning fields.
                   // we still want to fallback to reflection as unsafe accessor is known to be unsupported on android/iOS.
                   // See type initialzer for more details.
                   return (logger_new_entry_field ??= typeof(Logger).GetField("NewEntry", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))?
                       .GetValue(null) is Action<LogEntry> del ? del : null;
               }))?
               .GetInvocationList()
               .Select(d => d.Target)
               .OfType<OsuGame>()
               .LastOrDefault();
    }

    private static bool IsInstantiatedFromRulesetStore()
    {
        try
        {
            var stackTrace = new StackTrace(2, false); // skip this frame and constructor frame
            var directCaller = stackTrace.GetFrame(0)?.GetMethod();

            // Exclude non-reflection creation.
            if (directCaller?.DeclaringType?.FullName?.StartsWith("System.") is not true)
                return false;

            var indirectCaller = stackTrace.GetFrame(1)?.GetMethod();

            // Check if any caller in the stack is from RulesetStore.
            return IsNestedTypeFromRulesetStore(indirectCaller?.DeclaringType);
        }
        catch
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        static bool IsNestedTypeFromRulesetStore(Type? type)
            => type != null &&
                (typeof(RulesetStore).IsAssignableFrom(type) ||
                (type.IsNested && IsNestedTypeFromRulesetStore(type.DeclaringType)));
    }

    public override string ShortName => "Plugins";
    public override string Description => "Provide plugin functionality";

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new DummyBeatmapConverter(beatmap, this);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new DummyDifficultyCalculator(RulesetInfo, beatmap);

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => throw new NotImplementedException("This ruleset is not meant to be played.");

    public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

    public override Drawable CreateIcon() => new OsuHook()
    {
        RelativeSizeAxes = Axes.Both,
        Content = new SpriteIcon
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Icon = FontAwesome.Solid.PuzzlePiece
        }
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
