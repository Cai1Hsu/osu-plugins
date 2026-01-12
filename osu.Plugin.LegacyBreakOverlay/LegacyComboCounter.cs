using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps.Timing;
using osu.Plugin.Legacy;
using osu.Game.Skinning;
using LazerLegacyCombo = osu.Game.Skinning.LegacyDefaultComboCounter;

namespace osu.Plugin.LegacyBreakOverlay;

public partial class LegacyComboCounter : BreakTrackingContainer, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;

    public bool UsesFixedAnchor { get; set; } = true;

    private Container counter;

    public LegacyComboCounter()
    {
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        AutoSizeAxes = Axes.Both;
        AlwaysPresent = true;

        InternalChild = counter = new Container()
        {
            AutoSizeAxes = Axes.Both,
            Child = new LazerLegacyCombo(),
        };
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

    private const float duration = 1000f;
    private const float offset_x = -80f * stable_ratio;

    private void slideOut()
    {
        counter
            .FadeOut(duration)
            .MoveToX(offset_x, duration, Easing.In);
    }

    private void slideIn()
    {
        counter
            .FadeIn(duration)
            .MoveToX(0, duration, Easing.Out);
    }
}
