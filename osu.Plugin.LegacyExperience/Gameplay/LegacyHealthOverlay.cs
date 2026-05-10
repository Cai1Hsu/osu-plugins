using osu.Framework.Graphics;
using osu.Game.Skinning;
using osu.Game.Beatmaps.Timing;
using osu.Framework.Graphics.Containers;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacyHealthOverlay : BreakTrackingContainer, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;

    public bool UsesFixedAnchor { get; set; } = true;

    private Container display;

    public LegacyHealthOverlay()
    {
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        AutoSizeAxes = Axes.Both;
        AlwaysPresent = true;

        InternalChild = display = new Container()
        {
            AutoSizeAxes = Axes.Both,
            Child = new LegacyHealthDisplay(),
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // they are awared of the fact that health overlay usually takes up the full screen area
        // but they simply did a ugly hack to make it work
        // see https://github.com/ppy/osu/blob/458a27c99a010310d75cce86ed5634250ff7bb15/osu.Game/Screens/Play/HUDOverlay.cs#L299-L303
        // use a custom and compute anchor position ourselves to avoid taking the full screen area
        // This fixes that chat display and other things being squeezed to the bottom of the screen when health overlay is enabled
        if (Anchor.HasFlagFast(Anchor.y0))
        {
            var relativeAnchorPosition = RelativeAnchorPosition;

            Anchor = Anchor.Custom;
            RelativeAnchorPosition = relativeAnchorPosition;
        }
    }

    protected override void ScheduleBreakAnimations(IReadOnlyList<BreakPeriod> breaks)
    {
        foreach (var period in breaks)
        {
            using (BeginAbsoluteSequence(period.StartTime))
            {
                slideOut();

                using (BeginDelayedSequence(period.Duration))
                    slideIn();
            }
        }
    }

    private const float duration = 500;
    private const float offset_y = -20 * stable_ratio;

    private void slideIn()
    {
        display
            .FadeIn(duration)
            .MoveToY(0, duration);
    }

    private void slideOut()
    {
        display
            .FadeOut(duration)
            .MoveToY(offset_y, duration);
    }
}
