// This file is adapted from osu!lazer's LegacySpriteText to support more legacy skin fonts.
// Original file: https://github.com/ppy/osu/blob/master/osu.Game/Skinning/LegacySpriteText.cs

using System.Collections.Frozen;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Plugins.Legacy;

public partial class LegacySpriteText : OsuSpriteText
{
    public record CustomCharacterMapping(string lookupName, char character);

    public Vector2? MaxSizePerGlyph { get; init; }
    public bool FixedWidth { get; init; }

    private LegacyGlyphStore glyphStore = null!;

    protected override char FixedWidthReferenceCharacter => '5';

    protected override char[] FixedWidthExcludeCharacters => new[] { ',', '.', '%', 'x', '/' };

    private readonly string fontPrefix;
    public LegacySpriteText(string legacyFont)
    {
        this.fontPrefix = legacyFont;
    }

    public float FontOverlap
    {
        get => -Spacing.X;
        set => Spacing = new Vector2(-value, Spacing.Y);
    }

    public FrozenDictionary<char, string>? CustomMappings { get; init; }

    [BackgroundDependencyLoader]
    private void load(ISkinSource skin)
    {
        base.Font = new FontUsage(fontPrefix, 1, fixedWidth: FixedWidth);
        glyphStore = new LegacyGlyphStore(fontPrefix, skin, MaxSizePerGlyph, CustomMappings);

        // cache common lookups ahead of time.
        foreach (char c in FixedWidthExcludeCharacters)
            glyphStore.Get(fontPrefix, c);
        for (int i = 0; i < 10; i++)
            glyphStore.Get(fontPrefix, (char)('0' + i));
    }

    protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store) => base.CreateTextBuilder(glyphStore);

    private class LegacyGlyphStore : ITexturedGlyphLookupStore
    {
        private readonly ISkin skin;
        private readonly Vector2? maxSize;

        private readonly string fontName;

        private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();

        private FrozenDictionary<char, string>? customMappings;

        public LegacyGlyphStore(string fontName, ISkin skin, Vector2? maxSize, FrozenDictionary<char, string>? customMappings = null)
        {
            this.fontName = fontName;
            this.skin = skin;
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

            var texture = skin.GetTexture($"{fontName}-{lookup}");

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