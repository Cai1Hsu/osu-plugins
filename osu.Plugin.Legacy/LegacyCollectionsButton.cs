using osu.Framework.Graphics;
using osu.Game.Skinning;

namespace osu.Game.Plugins.Legacy;

public partial class LegacyCollectionsButton : LegacySpriteButton, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyCollectionsButton()
    {
        Texture = "UI/overlay-collections";

        Anchor = Anchor.CentreRight;
        Origin = Anchor.CentreRight;

        // This button is currently no-op in terms of functionality, as the in-game collections overlay
        // has not yet been implemented in the new osu! client.
    }
}