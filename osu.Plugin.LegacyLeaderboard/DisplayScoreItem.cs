using osu.Framework.Bindables;
using osu.Framework.Graphics.Pooling;
using osu.Game.Screens.Select.Leaderboards;

namespace osu.Plugin.LegacyLeaderboard;

internal partial class DisplayScoreItem : IDisposable
{
    public PoolableDrawable? Model { get; set; }

    public GameplayLeaderboardScore GameplayScore { get; init; }

    public Bindable<int?> ScorePosition { get; private set; } = new Bindable<int?>();
    public Bindable<long> ProviderDisplayOrder { get; private set; } = new Bindable<long>();

    public Bindable<long> LeaderboardDisplayIndex { get; } = new Bindable<long>();
    public BindableBool VisibleInLeaderboard { get; } = new BindableBool(false);

    public float YPosition { get; set; }

    public DisplayScoreItem(GameplayLeaderboardScore score)
    {
        GameplayScore = score;

        ScorePosition.BindTo(score.Position);
        ProviderDisplayOrder.BindTo(score.DisplayOrder);
    }

    public void Dispose()
    {
        Model = null;

        ScorePosition.UnbindAll();
        ProviderDisplayOrder.UnbindAll();
    }
}
