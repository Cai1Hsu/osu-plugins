using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osuTK;

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// A <see cref="SpriteText"/> that tries to replicate the appearance of FontText in stable.
/// Consider using <see cref="LegacyFont" /> to get a <see cref="FontUsage"/> that best replicates the default font in stable.
/// Note that this is not a perfect replication, and may not look exactly the same as FontText in stable.
/// </summary>
public partial class FontText : SpriteText
{
    private new Vector2 ShadowOffset => new Vector2(
        Math.Max(0.5f, Math.Min(1f, Font.Size / (14 * LegacyExperiencePlugin.StableRatio))));

    protected override DrawNode CreateDrawNode() => new FontTextDrawNode(this);

    public FontText()
    {
        ShadowColour = new Colour4(0, 0, 0, 100);
    }

    protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store)
    {
        // lazer's CJK glyphs look smaller than stable's,
        // so we need to scale them up to match the appearance in stable.
        // This is a bit of a hack, but it works well enough for now.
        return base.CreateTextBuilder(new ScalingGlyphStore(store));
    }

    private partial class ScalingGlyphStore : ITexturedGlyphLookupStore
    {
        private readonly ITexturedGlyphLookupStore inner;

        public ScalingGlyphStore(ITexturedGlyphLookupStore inner)
        {
            this.inner = inner;
        }

        public ITexturedCharacterGlyph? Get(string? fontName, char character)
        {
            return scale(inner.Get(fontName, character));
        }

        public async Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character)
        {
            return scale(await inner.GetAsync(fontName, character));
        }

        private ITexturedCharacterGlyph? scale(ITexturedCharacterGlyph? glyph)
        {
            if (glyph is null)
                return null;

            if (!shouldScale(glyph.Character))
                return glyph;

            return new ScalingCharacterGlyph(glyph);
        }

        private static bool shouldScale(char c)
        {
            return checkCjkCharacter(c);
        }

        // the ranges here are based on the ranges used by NativeText in stable.
        private static bool checkCjkCharacter(char c)
        {
            return (c >= '一' && c <= '鿿') ||
                (c >= '㐀' && c <= '䷿') ||
                (c >= 131072 && c <= 173791) ||
                (c >= '豈' && c <= '\ufaff') ||
                (c >= 194560 && c <= 195103);
        }

        // TODO: the calculation here may be incorrect, but looks good enough for now.
        private class ScalingCharacterGlyph : ITexturedCharacterGlyph
        {
            public Texture Texture { get; }

            public float Width => inner.Width * scaleAdjust;

            public float Height => inner.Height * scaleAdjust;

            public float XOffset => inner.XOffset * scaleAdjust;

            public float YOffset => inner.YOffset * scaleAdjust;

            public float XAdvance => inner.XAdvance * scaleAdjust;

            public float Baseline => inner.Baseline * scaleAdjust;

            public char Character => inner.Character;

            public float GetKerning<T>(T lastGlyph) where T : ICharacterGlyph
                => inner.GetKerning(lastGlyph);

            private readonly ITexturedCharacterGlyph inner;

            // The value choose here is arbitrary, seems good enough to make CJK characters look similar to their stable.
            private const float scaleAdjust = 1.25f;

            public ScalingCharacterGlyph(ITexturedCharacterGlyph inner)
            {
                this.inner = inner;

                // create a copy to avoid modifying the original texture, which may be shared by multiple glyphs.
                Texture = inner.Texture.Crop(new RectangleF(Vector2.Zero, Vector2.One), Axes.Both);

                // ScaleAdjust is used to scale down for high DPI textures,
                // this is a bit of a hack to scale up the texture for CJK characters, which are too small in stable.
                Texture.ScaleAdjust = 1 / scaleAdjust;
            }
        }
    }
}
