using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Plugins.Legacy;
using osu.Game.Skinning;
using LazerLegacyCombo = osu.Game.Skinning.LegacyDefaultComboCounter;

namespace osu.Plugin.LegacyBreakOverlay;

public partial class LegacyComboCounter : BreakTrackingContainer, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;

    public bool UsesFixedAnchor { get; set; } = true;

    private LazerLegacyCombo combo = null!;

    public LegacyComboCounter()
    {
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        AutoSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChild = combo = new LazerLegacyCombo();
    }

    private const float duration = 1000f;
    private const float offset_x = -80f * stable_ratio;

    private void slideOut()
    {
        combo.FadeOut(duration)
            .MoveToX(offset_x, duration, Easing.In);
    }

    private void slideIn()
    {
        combo.FadeIn(duration)
            .MoveToX(0, duration, Easing.Out);
    }

    public override void OnBreakStart()
    {
        base.OnBreakStart();

        var currentBreak = CurrentBreak.Value;

        if (currentBreak is null)
            return;

        var period = currentBreak.Value;

        using (BeginAbsoluteSequence(period.Start))
        {
            slideOut();

            using (BeginDelayedSequence(period.Duration))
                slideIn();
        }
    }
}
