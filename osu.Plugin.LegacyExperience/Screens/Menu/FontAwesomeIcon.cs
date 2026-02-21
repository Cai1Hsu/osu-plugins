using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Sprites;
using osu.Plugin.LegacyExperience.Graphics;
using osuTK;
using static osu.Plugin.LegacyExperience.Graphics.NativeText;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class FontAwesomeIcon : Sprite
{
    private float fontSize;

    public float FontSize
    {
        get => fontSize;
        set
        {
            if (fontSize == value)
                return;

            fontSize = value;

            if (LoadState >= LoadState.Loaded)
                updateTexture();
        }
    }

    private IconUsage icon;

    public IconUsage Icon
    {
        get => icon;
        set
        {
            if (icon.Equals(value))
                return;

            icon = value;

            // matches SpriteIcon
            if (LoadState > LoadState.NotLoaded)
                updateTexture();
        }
    }

    [Resolved]
    private INativeText nativeText { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        updateTexture();
    }

    private void updateTexture()
    {
        Texture? texture = null;
        Vector2 textureSize = Vector2.Zero;

        if (fontSize > 0)
        {
            string c = icon.Icon.ToString();

            nativeText.CreateText(new TextCreationParameters
            {
                Text = c,
                FontFace = LegacyFontFace.FontAwesome,
                Size = fontSize,
                RenderFlags = TextRenderFlags.Render,
            }, out var result);

            texture = result.Texture;
            textureSize = texture?.DisplaySize ?? Vector2.Zero;
        }

        Texture = texture;
        Size = textureSize;
    }
}
