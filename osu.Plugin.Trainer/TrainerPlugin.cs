using osu.Framework.Allocation;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Plugins;
using osu.Game.Plugins.Skins;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning;

namespace osu.Plugin.Trainer;

public class TrainerPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        var skinEditor = game.Dependencies.Get<SkinEditorOverlay?>();

        if (skinEditor is not null)
        {
            skinEditor.RegisterSkinComponents(new Type[]
            {
                typeof(AlternateTrainer),
            }, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, new OsuRuleset().RulesetInfo));
        }
    }
}
