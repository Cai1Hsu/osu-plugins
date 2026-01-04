using System.Diagnostics;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Game.Utils;

namespace osu.Game.Rulesets.PluginsLoader;

public static class GameExtensions
{
    private static readonly MethodInfo drawable_scheduler_getter = typeof(Drawable)
        .GetProperty("Scheduler", BindingFlags.NonPublic | BindingFlags.Instance)?
        .GetMethod!;

    private static readonly MethodInfo? game_storage_getter = typeof(OsuGameBase)
        .GetProperty("Storage", BindingFlags.NonPublic | BindingFlags.Instance)?
        .GetMethod;

    // On platforms like iOS/Android where UnsafeAccessor is not available,
    // InvokeWhenReady extension method failed to run platform warning and selector hook.
    private static void InvokeWhenReadyFallback(this Drawable d, Action<Drawable> action, bool requiresUpdateThread = true)
        => d.InvokeWhenReady(action, d => (Scheduler)drawable_scheduler_getter.Invoke(d, null)!, requiresUpdateThread);

    public static void PerformPluginsLoad(this OsuGame game)
    {
        Debug.Assert(drawable_scheduler_getter is not null);

        var pluginsManager = new PluginManager();

        try
        {
            var gameStorage = game_storage_getter?.Invoke(game, null) as Storage;

            // Get assemblies loaded to app domain so type resolution can occur correctly.
            pluginsManager.LoadEarlyAssemblies(gameStorage);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load early assemblies: {ex.Message}.", LoggingTarget.Runtime, LogLevel.Error);
        }

        // ensure the instance actually created before we try to access it.
        // Run this at first to avoid exceptions thrown during plugin loading being logged to Sentry.
        game.InvokeWhenReadyFallback(disableSentryLogging);

        // The new hook method runs so early that the game instance is still loading.
        // We need to delay here because the dependencies are not yet available.
        game.InvokeWhenReadyFallback(d =>
        {
            var notification = game.Dependencies.Get<INotificationOverlay>();
            var ruleset = game.Dependencies.Get<IBindable<RulesetInfo>?>();

            try
            {
                if (PluginLoaderRuleset.IsUnsupportedPlatforms)
                {
                    notification?.Post(new PluginNotification
                    {
                        Text = "Your platform is not supported due to some technical limitations. "
                            + "Some plugins will not function.",
                        Icon = FontAwesome.Solid.ExclamationTriangle,
                        IconColour = Colour4.Yellow,
                    });
                    return;
                }

                game.InjectDependencies(out PluginManager _, () => pluginsManager);
            }
            finally
            {
                hookRulesetSelector(ruleset);
            }
        });
    }

    private static readonly FieldInfo newEntry_event_field = typeof(Logger)
        // Backing field for event 'NewEntry'
        .GetField("NewEntry", BindingFlags.NonPublic | BindingFlags.Static)!;

    // Plugins may throw unhandled exceptions which get logged to Sentry.
    // So we disable logging to avoid spamming Sentry with errors.
    private static void disableSentryLogging(Drawable _)
    {
        Debug.Assert(newEntry_event_field is not null);

        try
        {
            var delegates = newEntry_event_field.GetValue(null) as Action<LogEntry>;

            if (delegates is null)
                return;

            var invocation_list = delegates.GetInvocationList()
                .Where(d => d.Target is SentryLogger)
                .ToArray();

            foreach (var sentryLoggingMethod in invocation_list)
            {
                Logger.NewEntry -= (Action<LogEntry>)sentryLoggingMethod;
            }
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to disable Sentry logging: {e.Message}", level: LogLevel.Important);
        }
    }

    private static void hookRulesetSelector(IBindable<RulesetInfo>? ruleset)
    {
        if (ruleset is not Bindable<RulesetInfo> bindableRuleset)
            return;

        var pluginRulesetInfo = new PluginLoaderRuleset(null).RulesetInfo;

        bindableRuleset.BindValueChanged(v =>
        {
            if (v.NewValue.Equals(pluginRulesetInfo))
            {
                var disabled = bindableRuleset.Disabled;
                bindableRuleset.Disabled = false;
                bindableRuleset.Value = v.OldValue;
                bindableRuleset.Disabled = disabled;

                // TODO: Fire an event, this means our ruleset is *clicked*.
            }
        });
    }
}