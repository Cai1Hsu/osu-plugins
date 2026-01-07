using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Plugins.Legacy;
using osu.Game.Skinning;
using osu.Game.Utils;
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

    public override void OnGameSeeked()
    {
        combo.ClearTransforms();

        base.OnGameSeeked();
    }

    public override void OnBreakEnd()
    {
        void playAnimation()
        {
            combo.FadeIn(duration)
                .MoveToX(0, duration, Easing.Out);
        }

        PlayAnimation(b =>
        {
            using (BeginAbsoluteSequence(b.End))
                playAnimation();
        }, () =>
        {
            playAnimation();
            combo.FinishTransforms();
        });
    }

    public override void OnBreakStart()
    {
        void playAnimation()
        {
            combo.FadeOut(duration)
                .MoveToX(offset_x, duration, Easing.In);
        }

        PlayAnimation(b =>
        {
            using (BeginAbsoluteSequence(b.Start))
                playAnimation();
        }, () =>
        {
            playAnimation();
            combo.FinishTransforms();
        });
    }
}
