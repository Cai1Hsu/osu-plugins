using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Sprites;
using osu.Game.Plugins.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboardEntry : CompositeDrawable
{
    private const float stable_ratio = 1.6f;

    // stable uses 0.62 but McOsu and Wieku/danser-go both use 0.625 for some reason.
    // Let's align with stable for now.
    private const float background_scale = 0.62f;
    private static readonly Vector2 background_offset = new Vector2(0, 20 * background_scale);

    private OsuSpriteText nameSprite = null!;
    private LegacySpriteTextContainer scoreSprite = null!;
    private LegacySpriteTextContainer comboSprite = null!;
    private LegacySpriteTextContainer rankSprite = null!;
    private Sprite backgroundSprite = null!;

    public LegacyLeaderboardEntry()
    {
        InternalChildren = new Drawable[]
        {
            backgroundSprite= new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Scale = new Vector2(background_scale),
            },
            // TODO: use stable's font
            nameSprite = new OsuSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Font= new FontUsage(size: 14f, fixedWidth: false),
                Scale = new Vector2(stable_ratio),
                Position = background_offset +new Vector2(2.5f, -2f) * stable_ratio,
                AllowMultiline = false,
                Text = "PlayerName",
            },
            scoreSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                FixedWidth = true,
                Position = background_offset+new Vector2(2f, 18f) * stable_ratio,
                Colour = Color4.White,
                SpriteText =
                {
                    FontOverlap = 2.5f * stable_ratio,
                }
            },
            comboSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                FixedWidth = true,
                Colour = new Color4(153, 251, 255, 255),
                Position = background_offset + new Vector2(0, 18f) * stable_ratio,
                SpriteText =
                {
                    FontOverlap = 2.5f * stable_ratio,
                }
            },
            rankSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = background_offset + new Vector2(0, -2f) * stable_ratio,
                Colour = new Color4(255, 255, 255, 80),
                Scale = new Vector2(2.2f),
                SpriteText =
                {
                    FontOverlap = 3f,
                }
            }
        };
    }

    [BackgroundDependencyLoader]
    private void load(ISkinSource skin)
    {
        // background vertical offset + 18 for score/combos Y + 14 for score/combos height
        float height = 103 * background_scale; // default sprite's height is 103
        float width = 82 * stable_ratio;

        Size = new Vector2(width, height);

        Texture? getCroppedBackground()
        {
            Texture? texture = skin.GetTexture("menu-button-background");

            if (texture is null)
                return null;

            Vector2 cropAt = new Vector2(470 * texture.ScaleAdjust, 0);
            Vector2 textureSize = texture.Size;

            var cropped = texture.Crop(new RectangleF(cropAt, textureSize - cropAt));
            cropped.ScaleAdjust = texture.ScaleAdjust;

            return cropped;
        }

        backgroundSprite.Texture = getCroppedBackground();
    }
}
