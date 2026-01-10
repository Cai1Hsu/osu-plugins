// This file is adapted from osu!lazer's LegacySpriteText to support more legacy skin fonts.
// Original file: https://github.com/ppy/osu/blob/master/osu.Game/Skinning/LegacySpriteText.cs

using System.Collections.Frozen;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Plugins.Legacy;

/// <summary>
/// A sprite text which uses legacy osu! font textures.
/// </summary>
public partial class LegacySpriteText : OsuSpriteText
{
    public Vector2? MaxSizePerGlyph { get; set; }
    public bool FixedWidth { get; set; }

    public delegate Texture? TextureLookupDelegate(string lookupName);

    private ITexturedGlyphLookupStore glyphStore = null!;

    private readonly string fontPrefix;

    public TextureLookupDelegate? TextureLookup { get; set; }

    public FrozenDictionary<char, string>? CustomMappings { get; set; }

    public LegacySpriteText(string fontPrefix)
    {
        this.fontPrefix = fontPrefix;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (TextureLookup is null)
            throw new InvalidOperationException($"{nameof(TextureLookup)} must be provided when creating {nameof(LegacySpriteText)}.");

        glyphStore = new LegacyGlyphStore(fontPrefix, TextureLookup, MaxSizePerGlyph, CustomMappings);
        base.Font = new FontUsage(fontPrefix, 1, fixedWidth: FixedWidth);

        // cache common lookups ahead of time.
        foreach (char c in FixedWidthExcludeCharacters)
            glyphStore.Get(fontPrefix, c);
        for (int i = 0; i < 10; i++)
            glyphStore.Get(fontPrefix, (char)('0' + i));
    }

    protected override char FixedWidthReferenceCharacter => fixedWidthReferenceCharacter;

    protected override char[] FixedWidthExcludeCharacters => defaultFixedWidthExcludeCharacters;


    private static readonly char[] defaultFixedWidthExcludeCharacters = new[] { ',', '.', '%', 'x', '/' };

    private char fixedWidthReferenceCharacter = '0';
    protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore _)
        => base.CreateTextBuilder(glyphStore);

    public float FontOverlap
    {
        get => -Spacing.X;
        set => Spacing = new Vector2(-value, Spacing.Y);
    }

    internal class LegacyGlyphStore : ITexturedGlyphLookupStore
    {
        private readonly TextureLookupDelegate textureLookup;
        private readonly Vector2? maxSize;

        private readonly string fontName;

        private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();

        private FrozenDictionary<char, string>? customMappings;

        public LegacyGlyphStore(string fontName, TextureLookupDelegate textureLookup, Vector2? maxSize, FrozenDictionary<char, string>? customMappings = null)
        {
            ArgumentNullException.ThrowIfNull(textureLookup);

            this.fontName = fontName;
            this.textureLookup = textureLookup;
            this.maxSize = maxSize;
            this.customMappings = customMappings;
        }

        public ITexturedCharacterGlyph? Get(string? fontName, char character)
        {
            // We only service one font.
            if (fontName != this.fontName)
                return null;

            if (cache.TryGetValue(character, out var cached))
                return cached;

            string lookup = getLookupName(character);

            var texture = textureLookup($"{fontName}-{lookup}");

            TexturedCharacterGlyph? glyph = null;

            if (texture != null)
            {
                if (maxSize != null)
                    texture = texture.WithMaximumSize(maxSize.Value);

                glyph = new TexturedCharacterGlyph(new CharacterGlyph(character, 0, 0, texture.Width, 0, null), texture, 1f / texture.ScaleAdjust);
            }

            cache[character] = glyph;
            return glyph;
        }

        private string getLookupName(char character)
        {
            if (customMappings != null && customMappings.TryGetValue(character, out var mapping))
                return mapping;

            switch (character)
            {
                case ',':
                    return "comma";

                case '.':
                    return "dot";

                case '%':
                    return "percent";

                default:
                    return character.ToString();
            }
        }

        public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.Run(() => Get(fontName, character));
    }
}
