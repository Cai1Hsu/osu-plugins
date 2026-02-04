using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.SelectV2;
using osuTK;
using SongSelectV2 = osu.Game.Screens.SelectV2.SongSelect;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyBeatmapPanel : LegacyPanelHasBeatmap
{
    private OsuSpriteText difficultyText = null!;
    private StarDifficultyDisplay? starDisplay;

    [Resolved]
    private BeatmapDifficultyCache? difficultyCache { get; set; }

    protected override Drawable CreatePlayInfo()
    {
        // in lazer, there's no case where play mode icon can be shown in legacy panel.
        return new LegacyLocalRankDisplay
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            LocalBestScore = { BindTarget = LocalBestScore },
        };
    }

    protected override Drawable[] CreateBeatmapInfoChildren() =>
    [
        ..base.CreateBeatmapInfoChildren(),
        difficultyText = new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = OsuFont.GetFont(size: 12f * LegacyExperiencePlugin.StableRatio, weight: FontWeight.Bold),
            Position = new Vector2(1, 4f) * LegacyExperiencePlugin.StableRatio,
            AllowMultiline = false,
            Colour = PanelColors.InactiveText,
            // Text = "Difficulty Name",
        },
        starDisplay = new StarDifficultyDisplay()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.TopLeft,
            // stable uses 7 as Y offset.
            // We tweaked it to 12 for better visual alignment in lazer
            Position = new Vector2(0, 12f) * LegacyExperiencePlugin.StableRatio,
        }
    ];

    protected override void UpdatePanelState(bool isActivated, bool activatedBySet)
    {
        base.UpdatePanelState(isActivated, activatedBySet);

        var difficultyColor = isActivated ? PanelColors.ActiveText : PanelColors.InactiveText;

        difficultyText.Colour = difficultyColor;
        starDisplay?.UpdateStarColor(difficultyColor, additive: !isActivated);
    }

    protected override void FreeAfterUse()
    {
        LocalBestScore.Value = null;

        difficultyText.Text = string.Empty;
        clearStarDifficultyComputation();

        base.FreeAfterUse();
    }

    protected override void PrepareForUse()
    {
        var playBeatmap = ((GroupedBeatmap)Item!.Model).Beatmap;

        difficultyText.Text = playBeatmap.DifficultyName;

        computeStarRating(playBeatmap);

        base.PrepareForUse();
    }

    private IBindable<StarDifficulty>? starDifficultyBindable;
    private CancellationTokenSource? starDifficultyCancellationSource;

    private void clearStarDifficultyComputation()
    {
        starDifficultyCancellationSource?.Cancel();
        starDifficultyCancellationSource = null;

        starDifficultyBindable?.UnbindAll();
        starDifficultyBindable = null;
    }

    private void computeStarRating(BeatmapInfo beatmap)
    {
        clearStarDifficultyComputation();

        starDifficultyCancellationSource = new CancellationTokenSource();

        if (difficultyCache is null)
            return;

        starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelectV2.DIFFICULTY_CALCULATION_DEBOUNCE);
        starDifficultyBindable.BindValueChanged(starDifficulty =>
        {
            if (starDisplay is null)
                return;

            starDisplay.Current.Value = starDifficulty.NewValue.Stars;
        }, true);
    }

    protected override PanelDisplayPolicy CreateDisplayPolicy(object model)
    {
        var groupedBeatmap = (GroupedBeatmap)model;
        var beatmapInfo = groupedBeatmap.Beatmap;

        return new PanelDisplayPolicy(
            beatmapInfo.Metadata,
            // match stable behavior of picking the first beatmap in the set as cover if possible
            beatmapInfo.BeatmapSet?.Beatmaps.MinBy(b => b.OnlineID) ?? beatmapInfo
        );
    }

    public override MenuItem[]? ContextMenuItems
    {
        get
        {
            if (Item?.Model is GroupedBeatmap groupedBeatmap)
                return songSelect?.GetForwardActions(groupedBeatmap.Beatmap).ToArray()
                    ?? Array.Empty<MenuItem>();

            return Array.Empty<MenuItem>();
        }
    }
}
