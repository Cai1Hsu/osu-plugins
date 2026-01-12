using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Plugins.Legacy;

public partial class LegacySpriteButton : Button
{
    public const float FadeDuration = 100;

    protected Sprite Sprite { get; private set; } = null!;

    public string? Texture { get; set; }

    public Colour4 HoverColour { get; set; } = Colour4.Pink;

    public Colour4 NormalColour { get; set; } = Colour4.White;

    public virtual bool ApplyHoverEffect => true;

    [Resolved]
    private TextureStore? textures { get; set; }

    public PoolableSkinnableSample? HoverSample { get; set; }

    public PoolableSkinnableSample? ClickSample { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            Sprite = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }
        };

        AddInternal(HoverSample ??= new PoolableSkinnableSample(new SampleInfo("click-short")));
        AddInternal(ClickSample ??= new PoolableSkinnableSample(new SampleInfo("click-short-confirm")));

        SetTexture(Texture);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        Sprite.FadeColour(NormalColour);
    }

    protected override bool OnHover(HoverEvent e)
    {
        HoverSample?.Play();

        if (ApplyHoverEffect)
            Sprite.FadeColour(HoverColour, FadeDuration);

        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        // don't check ApplyHoverEffect to ensure normal colour applied
        Sprite.FadeColour(NormalColour, FadeDuration);

        base.OnHoverLost(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        // TODO: should we allow sample to be played when triggered from bindings?
        ClickSample?.Play();

        // match stable behaviour of fade hover effect when ApplyHoverEffect became false
        if (!ApplyHoverEffect)
            Sprite.FadeColour(NormalColour, FadeDuration);

        return base.OnClick(e);
    }

    protected Texture? GetTexture(string textureName)
    {
        return textures?.Get($"{textureName}@2x")
            ?? textures?.Get(textureName);
    }

    protected void SetTexture(string? textureName)
    {
        if (string.IsNullOrEmpty(textureName))
            return;

        Texture? texture = GetTexture(textureName);

        if (texture is not null)
            Sprite.Texture = texture;
    }
}
