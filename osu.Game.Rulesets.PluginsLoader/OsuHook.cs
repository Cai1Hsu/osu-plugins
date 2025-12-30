using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    private void load(OsuGame game, IRulesetConfigCache? rulesetConfig, IBindable<RulesetInfo>? ruleset)
    {
        // The intro may create icon for display purposes, which doesn't include dependencies we require for plugin loading.
        if (rulesetConfig == null || ruleset == null)
            return;

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

    private void hookRulesetSelector(Bindable<RulesetInfo>? ruleset)
    {
        if (ruleset is null)
            return;

        var pluginRulesetInfo = new PluginLoaderRuleset().RulesetInfo;

        ruleset.BindValueChanged(v =>
        {
            if (v.NewValue.Equals(pluginRulesetInfo))
            {
                var disabled = ruleset.Disabled;
                ruleset.Disabled = false;
                ruleset.Value = v.OldValue;
                ruleset.Disabled = disabled;

                // TODO: Fire an event, this means our ruleset is *clicked*.
            }
        });
    }
}
