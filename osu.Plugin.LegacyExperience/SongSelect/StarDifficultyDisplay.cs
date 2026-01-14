using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osuTK;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class StarDifficultyDisplay : PoolableDrawable
{
    [Resolved]
    private ISkinSource? skin { get; set; }

    [Resolved]
    private TextureStore? textures { get; set; }

    private const int star_count = 10;

    private Star[] stars = new Star[star_count];

    public ReadOnlySpan<Star> Stars => stars;

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;

        for (int i = 0; i < stars.Length; i++)
        {
            var star = new Star
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(i * 12, 0), // TODO: spacing
                Scale = new Vector2(0.35f), // TODO: investigate scale?
                Blending = BlendingParameters.Additive,
            };

            stars[i] = star;
        }

        InternalChildren = stars;

        if (skin is not null)
            skin.SourceChanged += onSkinChanged;

        onSkinChanged(); // update textures initially
    }

    private void onSkinChanged()
    {
        float offsetX = 0;

        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];
            var texture = getStarTexture(skin, textures);

            // we've packed a fallback texture, so this should never be null
            Debug.Assert(texture is not null);

            star.Texture = texture;

            // reposition stars based on texture size
            star.X = offsetX * LegacyExperiencePlugin.StableRatio;

            // Although textures are expected to be the same, we use the actual width to be safe.
            offsetX += texture.DisplayWidth * 0.625f * 0.6f;
        }
    }

    private static Texture? getStarTexture(ISkinSource? skin, TextureStore? textures)
    {
        return skin?.GetTexture("star")
            // fallback path for Argon skins
            ?? textures?.GetAutoSized("UI/star");
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= onSkinChanged;
    }

    public partial class Star : Sprite
    {
    }
}
