using osu.Framework.Graphics;
using osu.Game.Skinning;
using osu.Game.Plugins.Legacy;
using osu.Framework.Allocation;
using osu.Game.Beatmaps.Timing;

namespace osu.Plugin.LegacyBreakOverlay;

public partial class LegacyHealthOverlay : BreakTrackingContainer, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;

    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyHealthOverlay()
    {
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        AutoSizeAxes = Axes.Both;
        AlwaysPresent = true;

        InternalChild = new LegacyHealthDisplay();
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
        this.FadeIn(duration)
            .MoveToY(0, duration);
    }

    private void slideOut()
    {
        this.FadeOut(duration)
            .MoveToY(offset_y, duration);
    }
}
