using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Plugins.Skins.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;
using static osu.Game.Screens.Select.Leaderboards.GameplayLeaderboardScore;

namespace osu.Plugin.LegacyLeaderboard.Tests;

public partial class TestSceneLegacyLeaderBoardEntry : LocalSkinTestScene
{
    private static readonly Ruleset ruleset = new OsuRuleset();

    [Cached(typeof(ScoreProcessor))]
    private ScoreProcessor scoreProcessor = new ScoreProcessor(ruleset);

    [Cached(typeof(GameplayState))]
    private GameplayState gameplayState = new GameplayState(createBeatmap(), ruleset);

    private LegacyLeaderboardEntry entry = null!;
    private GameplayLeaderboardScore score = null!;

    private ComboDisplayMode comboDisplayMode = ComboDisplayMode.Current;

    public override string LocalSkinPath
        => @"C:\Users\Caiyi Hsu\AppData\Local\osu!\Skins\- 東方Project Youmu Konpaku - - Copy - Copy";
    // => @"C:\Users\Caiyi Hsu\AppData\Local\osu!\Skins\- # 『NM2』 Hatsune Miku 2.0 ~";

    [Resolved]
    private FontStore fontStore { get; set; } = null!;

    [SetUpSteps]
    public override void SetUpSteps()
    {
        base.SetUpSteps();

        AddToggleStep("Toggle combo mode", v =>
        {
            comboDisplayMode = v ? ComboDisplayMode.Highest : ComboDisplayMode.Current;
            recreateEntry();
        });
    }

    private void recreateEntry()
    {
        score = new GameplayLeaderboardScore(gameplayState, true, comboDisplayMode);

        SkinContainer.Clear();

        SkinContainer.Add(entry = new LegacyLeaderboardEntry(score)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });


        SkinContainer.Add(new SpriteText()
        {
            Font = new FontUsage("Aller", 36, weight: "Light"),
            Text = $"Combo Display Mode: {comboDisplayMode}",
        });
        
        SkinContainer.Add(new SpriteText()
        {
            Font = new FontUsage("Allemdasdasdmalsm", 36, weight: "Light"),
            Text = $"Combo Display Mode: {comboDisplayMode}",
        });
    }

    private static Beatmap createBeatmap() => new Beatmap()
    {
        HitObjects = new()
        {
            new HitObject()
        }
    };
}