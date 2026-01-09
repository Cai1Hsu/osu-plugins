using osu.Framework.Allocation;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Plugins;
using osu.Game.Plugins.Legacy;
using osu.Game.Plugins.Skins;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyLeaderboard;

public class LegacyLeaderboardPlugin : OsuPlugin
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
            typeof(LegacyLeaderboard),
        }, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));
    }
}
