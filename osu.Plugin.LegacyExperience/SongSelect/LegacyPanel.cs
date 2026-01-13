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

    protected LegacyPanelColors PanelColors { get; set; } = null!;

    private Sprite backgroundSprite = null!;

    [BackgroundDependencyLoader]
    private void load(LegacyPanelColors colours)
    {
        PanelColors = colours;

        AutoSizeAxes = Axes.Both;

        AddInternal(backgroundSprite = new Sprite()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Scale = new Vector2(TextureScale),
            // TODO: investigate how background's color is determined
        });

        skinChanged();

        if (skin is not null)
            skin.SourceChanged += skinChanged;
    }

    void skinChanged()
    {
        // TODO: Song select requires dynamic textures loading when skin changes
        // SkinnableSprite doestn't scale with @2x, so we manually retrieve the texture here.
        // This is a temporary workaround to make size correct.
        var texture = skin?.GetTexture("menu-button-background");

        const float background_fade_duration = 100;

        if (texture is not null)
        {
            backgroundSprite.FadeIn(background_fade_duration);
            backgroundSprite.Texture = texture;
            backgroundSprite.Size = texture.DisplaySize;
        }
        else // will this happen? we have a default texture right?
        {
            backgroundSprite.FadeOut(background_fade_duration);
            backgroundSprite.Size = new Vector2(799, 103) * TextureScale; // default texture size
        }
    }

    public virtual void Activated()
    {
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= skinChanged;
    }
}
