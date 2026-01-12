using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyExperience.Buttons;

public partial class LegacyChatButton : LegacyOverlayButton, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyChatButton()
    {
        TextureVisible = "UI/overlay-hide"; // when overlay is shown, this button indicates it can be hidden
        TextureHidden = "UI/overlay-show";
    }

    [BackgroundDependencyLoader]
    private void load(ChatOverlay? chatOverlay)
    {
        if (chatOverlay is not null)
            OverlayVisibility.BindTo(chatOverlay.State);
    }
}
