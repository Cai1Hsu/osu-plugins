using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Skinning;
using static osu.Plugin.LegacyExperience.LegacySpriteText;

namespace osu.Plugin.LegacyExperience;

/// <summary>
/// A size maintaining container for <see cref="LegacySpriteText"/>.
/// </summary>
public partial class LegacySpriteTextContainer : Container
{
    public record CustomCharacterMapping(string lookupName, char character);

    protected virtual TextureLookupDelegate CreateTextureLookup(IReadOnlyDependencyContainer dependencies)
    {
        var skin = dependencies.Get<ISkinSource>()
            ?? throw new InvalidOperationException($"No {nameof(ISkinSource)} available in the dependency container.");
        return skin.GetTexture;
    }

    protected virtual LegacySpriteText CreateSpriteText(string fontPrefix)
        => new LegacySpriteText(fontPrefix);

    private readonly string fontPrefix;
    public LegacySpriteTextContainer(string legacyFont)
    {
        fontPrefix = legacyFont;
        spriteText = CreateSpriteText(fontPrefix);
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

    private LegacySpriteText spriteText = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.X;

        spriteText.TextureLookup = CreateTextureLookup(Dependencies);

        AddInternal(spriteText);

        spriteText.Anchor = Anchor.TopLeft;
        spriteText.Origin = Anchor.TopLeft;
    }
}
