using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osu.Game.Utils;
using osuTK;

namespace osu.Plugin.Trainer.ShadowPlay;

internal partial class ShadowPlayButton : OsuAnimatedButton
{
    public readonly Bindable<ScoreInfo?> SelectedScore = new Bindable<ScoreInfo?>();
    public readonly Bindable<DownloadState> ReplayDownloadState = new Bindable<DownloadState>();

    private Box background = null!;

    [Resolved]
    private OsuColour colours { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Size = new Vector2(50, 30);

        Children = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Video,
            }
        };

        // should we schedule it?
        SelectedScore.BindValueChanged(_ => updateEnabledState());
        ReplayDownloadState.BindValueChanged(_ => updateEnabledState(), true);
    }

    private void updateEnabledState()
    {
        var enabled = SelectedScore.Value is { } score
            && ReplayDownloadState.Value is DownloadState.LocallyAvailable
            // obviously, only osu!std is is support since we are using mouse
            && score.RulesetID is 0;

        Enabled.Value = enabled;

        background.Colour = enabled ? colours.Green : colours.Gray8;
    }

    [Resolved]
    private ScoreManager scoreManager { get; set; } = null!;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        Action = () =>
        {
            var selectedScore = SelectedScore.Value;

            if (selectedScore is null)
                return;

            Score? databasedScore;

            try
            {
                databasedScore = scoreManager.GetScore(selectedScore);

                if (databasedScore is null)
                    throw new InvalidOperationException("Selected score is not available in the database.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to retrieve score from database.");
                return;
            }

            try
            {
                attachModAndStartGameplay(databasedScore);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start Shadow Play gameplay.");
            }
        };
    }

    [Resolved]
    private OsuGame game { get; set; } = null!;

    [Resolved]
    private Bindable<RulesetInfo> ruleset { get; set; } = null!;

    [Resolved]
    private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

    [Resolved]
    private BeatmapManager beatmapManager { get; set; } = null!;

    [Resolved]
    private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

    private void attachModAndStartGameplay(Score replayScore)
    {
        var spMod = new ShadowPlayMod(replayScore);
        var targetMods = replayScore.ScoreInfo.Mods
                                        .Select(m => m.DeepClone())
                                        .Append(spMod)
                                        .ToList();

        // we may want user selected mods to be applied as well, such as NoFail
        targetMods = selectedMods.Value.Concat(targetMods)
                                       .DistinctBy(m => m.Acronym) // in case of duplicate mods, keep the ones from selectedMods since they are more likely to be intentional by the user.
                                       .ToList();

        if (!ModUtils.CheckValidForGameplay(targetMods, out var invalidMods))
            invalidMods.ForEach(m => targetMods.Remove(m));

        var beatmap = replayScore.ScoreInfo.BeatmapInfo;

        game.PerformFromScreen(screen =>
        {
            Debug.Assert(screen.ValidForPush, $"Current screen {screen} is not valid for push, cannot start gameplay.");

            Logger.Log($"{nameof(attachModAndStartGameplay)} updating beatmap ({beatmap}) and ruleset ({replayScore.ScoreInfo.Ruleset}) to match score");

            if (!ruleset.Value.Equals(replayScore.ScoreInfo.Ruleset))
                ruleset.Value = replayScore.ScoreInfo.Ruleset;

            if (!this.beatmap.Value.BeatmapInfo.Equals(beatmap))
                this.beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmap);

            var mods = targetMods.ToImmutableArray();

            ((OsuScreen)screen).Mods.Value = mods;
            selectedMods.Value = mods;

            screen.Push(new PlayerLoader(createPlayer));

            // FIXME: keeping song select screen loses SP mod?
        });

        Player createPlayer() => new OfflinePlayer(playerConfiguration);
    }

    private static readonly PlayerConfiguration playerConfiguration = new PlayerConfiguration
    {
        ShowLeaderboard = true,
        ShowResults = true,
    };

    // A player that doesn't submit scores.
    // this is used since our mod is locally implemented and we don't want to mess with the player's actual score.
    private partial class OfflinePlayer : Player
    {
        [Cached(typeof(IGameplayLeaderboardProvider))]
        [SuppressMessage("CodeQuality", "IDE0052", Justification = "DI usage")]
        private readonly SoloGameplayLeaderboardProvider leaderboardProvider = new SoloGameplayLeaderboardProvider();

        public OfflinePlayer(PlayerConfiguration configuration = null!)
            : base(configuration)
        {
        }

        protected override ResultsScreen CreateResults(ScoreInfo score) => new SoloResultsScreen(score)
        {
            AllowRetry = true,
            IsLocalPlay = true,
        };
    }
}
