using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Pooling;
using osu.Game.Graphics.Carousel;
using osu.Game.Skinning;
using osuTK;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Plugins;
using osu.Framework.Input.Events;
using osu.Framework.Audio.Sample;
using osu.Game.Audio;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;

namespace osu.Plugin.LegacyExperience.SongSelect;

public abstract partial class LegacyPanel : PoolableDrawable, ICarouselPanel, IHasInitialPosition, IHasContextMenu
{
    internal const float TextureScale = 0.6f * 1.6f;

    public BindableBool Selected { get; private set; } = new BindableBool();

    public BindableBool Expanded { get; private set; } = new BindableBool();

    public BindableBool KeyboardSelected { get; private set; } = new BindableBool();

    // Legacy carousel managed, used for bypass Carousel's damping
    public double DrawYPosition { get; set; }
    public double? InitialXPosition { get; set; }

    public double SelectV2DrawYPosition
    {
        get => ((ICarouselPanel)this).DrawYPosition;
        set => ((ICarouselPanel)this).DrawYPosition = value;
    }

    double ICarouselPanel.DrawYPosition { get; set; }
    public CarouselItem? Item { get; set; }

    public override bool HandlePositionalInput => true;

    [Resolved]
    private ISkinSource? skin { get; set; }

    [Resolved]
    private TextureStore? textures { get; set; }

    protected BeatmapCarousel? Carousel { get; private set; }

    protected LegacyPanelColors PanelColors { get; set; } = null!;

    public virtual MenuItem[]? ContextMenuItems => null;

    private Sprite backgroundSprite = null!;

    [BackgroundDependencyLoader]
    private void load(LegacyPanelColors colours, BeatmapCarousel? carousel)
    {
        PanelColors = colours;
        Carousel = carousel;

        Anchor = Anchor.TopRight;
        Origin = Anchor.TopRight;
        AutoSizeAxes = Axes.Both;

        AddInternal(backgroundSprite = new Sprite()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Scale = new Vector2(TextureScale),
            // TODO: investigate how background's color is determined
        });

        SkinChanged();

        if (skin is not null)
            skin.SourceChanged += SkinChanged;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        Selected.BindValueChanged(_ => updateBackgroundColor());
        Expanded.BindValueChanged(_ => updateBackgroundColor());
        KeyboardSelected.BindValueChanged(v =>
        {
            UpdateBackgroundColor(getBackgroundColor(), 50);

            if (v.NewValue)
                flashBackground();
        });
    }

    private void flashBackground()
    {
        var color = LegacyPanelColors.Lighten2(getBackgroundColor(), 0.3f);
        backgroundSprite.FlashColour(color, 1000);
    }

    private void updateBackgroundColor(int duration = 300)
    {
        var targetColor = getBackgroundColor();

        UpdateBackgroundColor(targetColor, duration);
    }

    private ISample? hoverSample;
    private static readonly SampleInfo menu_click_sample_info = new SampleInfo("menuclick");

    protected virtual void SkinChanged()
    {
        hoverSample = skin?.GetSample(menu_click_sample_info);

        // TODO: Song select requires dynamic textures loading when skin changes
        // SkinnableSprite doesn't scale with @2x, so we manually retrieve the texture here.
        // This is a temporary workaround to make size correct.
        var texture = skin.GetSkinTexture("menu-button-background", textures, "UI");

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

    protected override bool OnHover(HoverEvent e)
    {
        // quick scrolling spamms hover events, so we suppress the sound in that case.
        if (Carousel?.RequestPlayPanelHoverSample() ?? true)
            hoverSample?.Play();

        flashBackground();
        return base.OnHover(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (Item is null || Carousel is null)
            return base.OnClick(e);

        Carousel.Activate(Item);
        return true;
    }

    public virtual void Activated()
    {
        backgroundSprite.FlashColour(PanelColors.White, 200, Easing.Out);
    }

    protected override void PrepareForUse()
    {
        // returning to pool makes it invisible, so fade in on next use.
        this.FadeIn();

        if (InitialXPosition is double xPos)
            X = (float)xPos;

        updateBackgroundColor(0);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= SkinChanged;
    }

    private Colour4 getBackgroundColor()
    {
        var color = GetBackgroundColor();

        if (KeyboardSelected.Value)
            color = color.Lighten(0.4f);

        return color;
    }

    protected abstract Colour4 GetBackgroundColor();

    protected void UpdateBackgroundColor(Colour4 colour, int duration = 300)
    {
        backgroundSprite.FadeColour(colour, duration);
    }
}
