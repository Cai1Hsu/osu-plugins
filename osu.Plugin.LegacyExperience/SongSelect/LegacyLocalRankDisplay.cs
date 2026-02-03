using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Scoring;
using static osu.Plugin.LegacyExperience.SongSelect.LegacyRankSpritePool;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyLocalRankDisplay : CompositeDrawable
{
    [Resolved]
    private LegacyRankSpritePool? rankSpritePool { get; set; }

    public Bindable<ScoreInfo?> LocalBestScore { get; } = new Bindable<ScoreInfo?>();

    private LegacyRankSprite? rankSprite;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Y;
        AutoSizeAxes = Axes.X;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        LocalBestScore.BindValueChanged(v => updateRankDisplay(v.NewValue), true);
    }

    private void updateRankDisplay(ScoreInfo? topScore)
    {
        if (rankSprite?.Rank == topScore?.Rank)
            return;

        if (rankSprite is not null)
        {
            RemoveInternal(rankSprite, false);
            rankSprite = null;
        }

        // Failed scores and no scores do not get a rank displayed.
        if (topScore is null || topScore.Rank < ScoreRank.D)
            return;

        rankSprite = getRankSprite(topScore.Rank);
        AddInternal(rankSprite);
    }

    private LegacyRankSprite getRankSprite(ScoreRank rank)
    {
        var sprite = rankSpritePool?.Get(rank)
            ?? new LegacyRankSprite(rank); // fallback in case the pool is not available, e.g. test scenarios

        sprite.Anchor = Anchor.Centre;
        sprite.Origin = Anchor.Centre;

        return sprite;
    }
}
