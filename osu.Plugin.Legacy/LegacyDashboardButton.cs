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

    [BackgroundDependencyLoader]
    private void load(DashboardOverlay? dashboardOverlay)
    {
        if (dashboardOverlay is not null)
            OverlayVisibility.BindTo(dashboardOverlay.State);

        OverlayVisibility.BindValueChanged(v =>
        {
            NormalColour = v.NewValue is Visibility.Visible ? Color4.White : Color4.Gray;

            // when user closed the overlay in other way, ensure the button colour is updated.
            Sprite.FadeColour(NormalColour, FadeDuration);
        }, true);
    }
}
