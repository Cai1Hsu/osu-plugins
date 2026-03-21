using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace osu.Plugin.LegacyExperience.Graphics;

public partial class LegacyTextFlowContainer : TextFlowContainer
{
    public LegacyTextFlowContainer(Action<SpriteText>? defaultCreationParameters = null)
        : base(defaultCreationParameters)
    {
    }

    public new Drawable InnerFlow => Flow;

    protected override SpriteText CreateSpriteText() => new FontText();
}
