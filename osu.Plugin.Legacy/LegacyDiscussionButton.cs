using osu.Framework.Graphics;
using osu.Game.Skinning;

namespace osu.Plugin.Legacy;

public partial class LegacyDiscussionButton : LegacySpriteButton, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyDiscussionButton()
    {
        Texture = "UI/overlay-discussion";

        Anchor = Anchor.CentreRight;
        Origin = Anchor.CentreRight;

        // This button is currently no-op in terms of functionality, as the discussion overlay
        // has not yet been implemented in the new osu! client.
    }
}