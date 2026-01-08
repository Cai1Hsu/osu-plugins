using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Skinning;

namespace osu.Game.Plugins.Legacy;

public partial class LegacyChatButton : LegacyOverlayButton, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyChatButton()
    {
        DefaultTexture = "UI/overlay-show"; // default state, chat is hidden
        ToggledTexture = "UI/overlay-hide";
    }

    [BackgroundDependencyLoader]
    private void load(ChatOverlay? chatOverlay)
    {
        if (chatOverlay is not null)
            OverlayVisibility.BindTo(chatOverlay.State);
    }
}
