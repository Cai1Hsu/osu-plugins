using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Plugins.Legacy;

public partial class LegacyDashboardButton : LegacyOverlayButton, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyDashboardButton()
    {
        TextureVisible = "UI/overlay-online";
        TextureHidden = "UI/overlay-online";
    }

    public override bool ApplyHoverEffect => State.Value is Visibility.Visible;

    [BackgroundDependencyLoader]
    private void load(DashboardOverlay? dashboardOverlay)
    {
        if (dashboardOverlay is not null)
            OverlayVisibility.BindTo(dashboardOverlay.State);

        OverlayVisibility.BindValueChanged(v =>
        {
            if (v.NewValue is Visibility.Hidden)
                Sprite.FadeColour(Color4.Gray, FadeDuration);
            else
                Sprite.FadeColour(NormalColour, FadeDuration);
        }, true);

        Sprite.FinishTransforms(); // ensure colour is applied immediately
    }
}
