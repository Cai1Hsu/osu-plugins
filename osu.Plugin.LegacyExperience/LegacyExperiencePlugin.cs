using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Buttons;
using osu.Plugin.LegacyExperience.Gameplay;
using osu.Plugin.LegacyExperience.Leaderboards;
using osu.Plugin.LegacyExperience.Online;

namespace osu.Plugin.LegacyExperience;

public sealed partial class LegacyExperiencePlugin : OsuPlugin
{
    public const float StableRatio = 1.6f;

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        gameBase.EnsureLegacyDependencies();

        if (gameBase is not OsuGame game)
            return;

        hookMainMenu(game);
        hookSongSelectScreen(game);
    }

    static LegacyExperiencePlugin()
    {
        var osuRuleset = new RulesetInfo
        {
            ShortName = "osu",
            OnlineID = 0,
            Available = true,
        };

        SkinEditorExtensions.RegisterSkinComponents(new[]
        {
            typeof(LegacyBreakOverlay),
            // In stable, only osu!standard has the break overlay.
        }, new SkinComponentContainerLookup(GlobalSkinnableContainers.MainHUDComponents, osuRuleset));

        SkinEditorExtensions.RegisterSkinComponents(new[]
        {
            typeof(LegacyHealthOverlay),
            typeof(LegacyComboCounter),
            typeof(LegacyErrorMeter),
            typeof(LegacyFpsDisplay),
            typeof(LegacyLeaderboard),
            typeof(PlayfieldMask),
            typeof(LegacyStoryboardExtend),
            typeof(LegacySkipOverlay),
        }, new SkinComponentContainerLookup(GlobalSkinnableContainers.MainHUDComponents));

        SkinEditorExtensions.RegisterSkinComponents(new[]
        {
            typeof(LegacyChatButton),
            typeof(LegacyDashboardButton),
            typeof(LegacyFpsDisplay),
            typeof(LegacyLocalUser),
        }, new SkinComponentContainerLookup(GlobalSkinnableContainers.SongSelect));
    }
}
