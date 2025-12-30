using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Plugins;

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
