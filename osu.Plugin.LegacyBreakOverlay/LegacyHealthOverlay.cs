using osu.Framework.Graphics;
using osu.Game.Skinning;
using osu.Game.Plugins.Legacy;
using osu.Framework.Allocation;

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

    public override void OnBreakEnd()
    {
        // Slide in
        healthDisplay
            .FadeIn(duration)
            .MoveToY(0, duration);
    }

    public override void OnBreakStart()
    {
        healthDisplay
            .FadeOut(duration)
            .MoveToY(offset_y, duration);
    }
}
