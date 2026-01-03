using osu.Framework.Allocation;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Plugins;
using osu.Game.Plugins.Skins;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyBreakOverlay;

public class LegacyBreakOverlayPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        OsuGame game = (OsuGame)gameBase;
        SkinEditorOverlay? skinEditor = game.Dependencies.Get<SkinEditorOverlay>();

        if (skinEditor is null)
            return;

        var osuRuleset = new RulesetInfo
        {
            ShortName = "osu",
            OnlineID = 0,
        };

        skinEditor.RegisterSkinComponents(
        new[]
        {
            typeof(LegacyBreakOverlay),
        },
        // In stable, only osu!standard has the break overlay.
        new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, osuRuleset));
    }
}
