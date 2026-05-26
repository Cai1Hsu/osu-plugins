using osu.Game.Plugins;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning;

namespace osu.Plugin.Trainer;

public class TrainerPlugin : OsuPlugin
{
    static TrainerPlugin()
    {
        SkinEditorExtensions.RegisterSkinComponents(new Type[]
        {
            typeof(AlternateTrainer),
        }, new SkinComponentContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new OsuRuleset().RulesetInfo));
    }
}
