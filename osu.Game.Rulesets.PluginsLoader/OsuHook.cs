using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Game.Utils;

namespace osu.Game.Rulesets.PluginsLoader;

partial class OsuHook : CompositeDrawable
{
    public Drawable Content
    {
        get => InternalChild;
        set => InternalChild = value;
    }

    [BackgroundDependencyLoader]
    private void load(OsuGame game, IRulesetConfigCache? rulesetConfig, IBindable<RulesetInfo>? ruleset, INotificationOverlay? notification)
    {
        // The intro may create icon for display purposes, which doesn't include dependencies we require for plugin loading.
        if (rulesetConfig == null || ruleset == null)
            return;

        if (PluginLoaderRuleset.IsUnsupportedPlatforms)
        {
            // FIXME: this will spam multiple notifications as the ruleset selector is created multiple times when the game launches.
            notification?.Post(new PluginNotification
            {
                Text = "Your platform is not supported due to some technical limitations. "
                    + "We don't have plan to provide support on this platform in the near future. "
                    + "Plugins will not functional.",
                Icon = FontAwesome.Solid.ExclamationTriangle,
                IconColour = Colour4.Red,
            });

            hookRulesetSelector(ruleset);
            return;
        }

        game.InvokeWhenReady(performHook);

        void performHook(Drawable d)
        {
            var game = (OsuGame)d;
            bool injected = game.InjectDependencies(out PluginManager _, () => new());

            if (injected)
            {
                hookRulesetSelector(ruleset as Bindable<RulesetInfo>);
            }

            // ensure the instance actually created before we try to access it.
            game.InvokeWhenReady(disableSentryLogging);
        }
    }

    // Plugins may throw unhandled exceptions which get logged to Sentry.
    // So we disable logging to avoid spamming Sentry with errors.
    private static void disableSentryLogging(Drawable d)
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "SentryLogger")]
        static extern ref SentryLogger GetSentryLogger(OsuGame game);

        if (d is not OsuGame game)
            return;

        var sentryLogger = GetSentryLogger(game);

        var sentryLoggingMethod = typeof(SentryLogger).GetMethod("processLogEntry", BindingFlags.NonPublic | BindingFlags.Instance);

        if (sentryLoggingMethod is null)
            return;

        try
        {
            // delegates are compared by method info + target instance
            Logger.NewEntry -= sentryLoggingMethod.CreateDelegate<Action<LogEntry>>(sentryLogger);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to disable Sentry logging: {e.Message}", level: LogLevel.Important);
        }
    }

    private void hookRulesetSelector(IBindable<RulesetInfo>? ruleset)
    {
        if (ruleset is not Bindable<RulesetInfo> bindableRuleset)
            return;

        var pluginRulesetInfo = new PluginLoaderRuleset().RulesetInfo;

        // ensure only single subscription
        // unsubscribe when not subscribed yet is safe
        bindableRuleset.ValueChanged -= onRulesetChanged;
        bindableRuleset.ValueChanged += onRulesetChanged;

        void onRulesetChanged(ValueChangedEvent<RulesetInfo> v)
        {
            if (v.NewValue.Equals(pluginRulesetInfo))
            {
                var disabled = bindableRuleset.Disabled;
                bindableRuleset.Disabled = false;
                bindableRuleset.Value = v.OldValue;
                bindableRuleset.Disabled = disabled;

                // TODO: Fire an event, this means our ruleset is *clicked*.
            }
        }
    }
}
