using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Pooling;
using osu.Game.Graphics.Carousel;
using osu.Game.Skinning;
using osuTK;
using osu.Framework.Graphics.Sprites;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyPanel : PoolableDrawable, ICarouselPanel
{
    internal const float TextureScale = 0.6f * 1.6f;

    public BindableBool Selected { get; private set; } = new BindableBool();

    public BindableBool Expanded { get; private set; } = new BindableBool();

    public BindableBool KeyboardSelected { get; private set; } = new BindableBool();

    // Legacy carousel managed, used for bypass Carousel's damping
    public double DrawYPosition { get; set; }

    public double OsuDrawYPosition
    {
        get => ((ICarouselPanel)this).DrawYPosition;
        set => ((ICarouselPanel)this).DrawYPosition = value;
    }

    double ICarouselPanel.DrawYPosition { get; set; }
    public CarouselItem? Item { get; set; }

    public override bool HandlePositionalInput => true;

    [Resolved]
    private ISkinSource? skin { get; set; }

    private Sprite backgroundSprite = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;

        AddInternal(backgroundSprite = new Sprite()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Scale = new Vector2(TextureScale),
        });

        if (skin is not null)
        {
            skin.SourceChanged += updateTexture;
            updateTexture();
        }
    }

    void updateTexture()
    {
        // TODO: Song select requires dynamic textures loading when skin changes
        // SkinnableSprite doestn't scale with @2x, so we manually retrieve the texture here.
        // This is a temporary workaround to make size correct.
        var texture = skin?.GetTexture("menu-button-background");

        if (texture is null)
            return;

        backgroundSprite.Texture = texture;
        backgroundSprite.Size = texture.DisplaySize;
    }

    public virtual void Activated()
    {
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= updateTexture;
    }
}
