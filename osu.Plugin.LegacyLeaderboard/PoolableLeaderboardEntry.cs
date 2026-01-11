using System.Diagnostics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Pooling;

namespace osu.Plugin.LegacyLeaderboard;

internal sealed partial class PoolableLeaderboardEntry : PoolableDrawable
{
    [Obsolete("Use the constructor with parameters.", true)]
    public PoolableLeaderboardEntry()
    {
        throw new InvalidOperationException("Use the constructor with parameters.");
    }

    public readonly LegacyLeaderboardEntry Drawable;

    public PoolableLeaderboardEntry(LegacyLeaderboardEntry drawableEntry)
    {
        AutoSizeAxes = Axes.Both;
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;

        InternalChild = Drawable = drawableEntry;
    }

    protected override void FreeAfterUse()
    {
        base.FreeAfterUse();

        Drawable.User = null;
        Drawable.ScorePosition.UnbindBindings();
        Drawable.ProviderDisplayOrder.UnbindBindings();
        Drawable.TotalScore.UnbindBindings();
        Drawable.Accuracy.UnbindBindings();
        Drawable.Combo.UnbindBindings();
        Drawable.HasQuit.UnbindBindings();
        Drawable.GetDisplayScore = null!;
        Drawable.IsTracking = false;

        if (boundScore is not null)
        {
            Debug.Assert(boundScore.Model == this);

            boundScore.Model = null;
            boundScore = null;
        }

        this.FadeOut();
        ClearTransforms(true);
    }

    private DisplayScoreItem? boundScore;

    public void BindScoreItem(DisplayScoreItem displayScore)
    {
        if (boundScore is not null)
            throw new InvalidOperationException($"This {nameof(PoolableLeaderboardEntry)} is already bound to a {nameof(DisplayScoreItem)}.");

        boundScore = displayScore;

        displayScore.Model = this;

        Drawable.ScorePosition.BindTo(displayScore.ScorePosition);
        Drawable.ProviderDisplayOrder.BindTo(displayScore.ProviderDisplayOrder);

        var score = displayScore.GameplayScore;

        // bind bindable states
        Drawable.User = score.User;
        Drawable.IsTracking = score.Tracked;
        Drawable.TotalScore.BindTo(score.TotalScore);
        Drawable.Accuracy.BindTo(score.Accuracy);
        Drawable.Combo.BindTo(score.Combo);
        Drawable.HasQuit.BindTo(score.HasQuit);
        Drawable.GetDisplayScore = score.GetDisplayScore;

        // sync states with display score
        Drawable.UpdatePanelState();
        Y = displayScore.YPosition;
    }
}