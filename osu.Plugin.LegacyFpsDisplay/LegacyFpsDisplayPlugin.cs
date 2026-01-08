using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Plugins;
using osu.Game.Plugins.Legacy;
using osu.Game.Plugins.Skins;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyFpsDisplay;

public class LegacyFpsDisplayPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        gameBase.EnsureLegacyResources();

        var game = (OsuGame)gameBase;
        SkinEditorOverlay? skinEditor = game.Dependencies.Get<SkinEditorOverlay?>();

        if (skinEditor is null)
            return;

        skinEditor.RegisterSkinComponents(new[]
        {
            typeof(LegacyFpsDisplay)
        }, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, null));
    }
}
