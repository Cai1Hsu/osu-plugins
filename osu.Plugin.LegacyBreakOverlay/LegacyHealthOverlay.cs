using osu.Framework.Graphics;
using osu.Game.Skinning;
using osu.Game.Plugins.Legacy;
using osu.Framework.Allocation;
using System.Diagnostics;

namespace osu.Plugin.LegacyBreakOverlay;

public partial class LegacyHealthOverlay : BreakTrackingContainer, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;

    public bool UsesFixedAnchor { get; set; } = true;

    private LegacyHealthDisplay healthDisplay = null!;

    public LegacyHealthOverlay()
    {
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        AutoSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChild = healthDisplay = new LegacyHealthDisplay();
    }

    private const float duration = 500;
    private const float offset_y = -20 * stable_ratio;

    public override void OnGameSeeked()
    {
        healthDisplay.ClearTransforms();

        base.OnGameSeeked();
    }

    public override void OnBreakEnd()
    {
        void playAnimation()
        {
            healthDisplay
                .FadeIn(duration)
                .MoveToY(0, duration);
        }

        PlayAnimation(b =>
        {
            using (BeginAbsoluteSequence(b.End))
                playAnimation();
        }, () =>
        {
            playAnimation();
            healthDisplay.FinishTransforms();
        });
    }

    public override void OnBreakStart()
    {
        void playAnimation()
        {
            healthDisplay
                .FadeOut(duration)
                .MoveToY(offset_y, duration);
        }

        PlayAnimation(b =>
        {
            using (BeginAbsoluteSequence(b.Start))
                playAnimation();
        }, () =>
        {
            playAnimation();
            healthDisplay.FinishTransforms();
        });
    }
}
