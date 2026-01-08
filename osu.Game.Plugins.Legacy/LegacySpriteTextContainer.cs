// This file is adapted from osu!lazer's LegacySpriteText to support more legacy skin fonts.
// Original file: https://github.com/ppy/osu/blob/master/osu.Game/Skinning/LegacySpriteText.cs

using System.Collections.Frozen;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Plugins.Legacy;

public partial class LegacySpriteTextContainer : Container
{
    public delegate Texture? TextureLookupDelegate(string lookupName);

    public record CustomCharacterMapping(string lookupName, char character);

    public Vector2? MaxSizePerGlyph { get; init; }
    public bool FixedWidth { get; init; }

    private readonly string fontPrefix;
    public LegacySpriteTextContainer(string legacyFont)
    {
        fontPrefix = legacyFont;

        InternalChild = spriteText = new CustomizableSpriteText()
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
        };
    }

    public FrozenDictionary<char, string>? CustomMappings { get; init; }

    protected virtual TextureLookupDelegate CreateTextureLookup(IReadOnlyDependencyContainer dependencies)
    {
        var skin = dependencies.Get<ISkinSource>()
            ?? throw new InvalidOperationException($"No {nameof(ISkinSource)} available in the dependency container.");
        return skin.GetTexture;
    }

    protected virtual void WithSpriteText(CustomizableSpriteText spriteText)
    {
    }

    public float FontHeight
    {
        get => Height;
        set => Height = value;
    }

    public LocalisableString Text
    {
        get => spriteText.Text;
        set => spriteText.Text = value;
    }

    public OsuSpriteText SpriteText => spriteText;

    private CustomizableSpriteText spriteText = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.X;

        var textureLookup = CreateTextureLookup(Dependencies);
        var glyphStore = new LegacyGlyphStore(fontPrefix, textureLookup, MaxSizePerGlyph, CustomMappings);

        spriteText.glyphStore = glyphStore;

        WithSpriteText(spriteText);

        spriteText.Font = new FontUsage(fontPrefix, 1, fixedWidth: FixedWidth);

        // cache common lookups ahead of time.
        foreach (char c in spriteText.FixedWidthExclude)
            glyphStore.Get(fontPrefix, c);
        for (int i = 0; i < 10; i++)
            glyphStore.Get(fontPrefix, (char)('0' + i));
    }

    public partial class CustomizableSpriteText : OsuSpriteText
    {
        internal ITexturedGlyphLookupStore glyphStore = null!;

        protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore _)
            => base.CreateTextBuilder(glyphStore);

        private readonly static char[] defaultFixedWidthExcludeCharacters = new[] { ',', '.', '%', 'x', '/' };

        private char fixedWidthReferenceCharacter = '0';
        private char[] fixedWidthExcludeCharacters = defaultFixedWidthExcludeCharacters;

        protected override char FixedWidthReferenceCharacter => FixedWidthReference;
        protected override char[] FixedWidthExcludeCharacters => FixedWidthExclude;

        public char FixedWidthReference
        {
            get => fixedWidthReferenceCharacter;
            set => fixedWidthReferenceCharacter = value;
        }

        public char[] FixedWidthExclude
        {
            get => fixedWidthExcludeCharacters;
            set => fixedWidthExcludeCharacters = value;
        }

        public float FontOverlap
        {
            get => -Spacing.X;
            set => Spacing = new Vector2(-value, Spacing.Y);
        }
    }

    private class LegacyGlyphStore : ITexturedGlyphLookupStore
    {
        private readonly TextureLookupDelegate textureLookup;
        private readonly Vector2? maxSize;

        private readonly string fontName;

        private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();

        private FrozenDictionary<char, string>? customMappings;

        public LegacyGlyphStore(string fontName, TextureLookupDelegate textureLookup, Vector2? maxSize, FrozenDictionary<char, string>? customMappings = null)
        {
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
