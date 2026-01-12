using osu.Framework.Allocation;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Plugins;
using osu.Game.Plugins.Skins;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Buttons;
using osu.Plugin.LegacyExperience.Gameplay;
using osu.Plugin.LegacyExperience.Leaderboards;

namespace osu.Plugin.LegacyExperience;

public sealed class LegacyExperiencePlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        gameBase.EnsureLegacyResources();

        if (gameBase is not OsuGame game)
            return;

        SkinEditorOverlay? skinEditor = game.Dependencies.Get<SkinEditorOverlay?>();
        if (skinEditor is null)
            return;

        var osuRuleset = new RulesetInfo
        {
            ShortName = "osu",
            OnlineID = 0,
            Available = true,
        };

        skinEditor.RegisterSkinComponents(new[]
        {
            typeof(LegacyBreakOverlay),
            // In stable, only osu!standard has the break overlay.
        }, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, osuRuleset));

        skinEditor.RegisterSkinComponents(new[]
        {
            typeof(LegacyHealthOverlay),
            typeof(LegacyComboCounter),
            typeof(LegacyErrorMeter),
            typeof(LegacyFpsDisplay),
            typeof(LegacyLeaderboard),
        }, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

        skinEditor.RegisterSkinComponents(new[]
        {
            typeof(LegacyChatButton),
            typeof(LegacyDashboardButton),
            typeof(LegacyFpsDisplay),
        }, new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect));
    }
}