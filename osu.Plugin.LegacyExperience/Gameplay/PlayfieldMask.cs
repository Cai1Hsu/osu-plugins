using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Layout;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Plugins;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osuTK;

namespace osu.Plugin.LegacyExperience.Gameplay;

/// <summary>
/// A mask which covers the playfield to maintain a 4:3 aspect ratio.
/// </summary>
public partial class PlayfieldMask : BreakTrackingContainer, ISerialisableDrawable
{
    public override bool HandleNonPositionalInput => false;
    public override bool HandlePositionalInput => false;

    public bool UsesFixedAnchor { get; set; } = true;

    [SettingSource("Fade out during breaks", "How the playfield mask should behave during breaks.")]
    public Bindable<FadeOutBehaviour> FadeOutDuringBreaks { get; private set; } = new Bindable<FadeOutBehaviour>()
    {
        Default = FadeOutBehaviour.Auto,
        Value = FadeOutBehaviour.Auto,
    };

    [SettingSource("Side border type", "The type of side borders to use.")]
    public Bindable<SideBorderType> VerticalBorderType { get; private set; } = new Bindable<SideBorderType>()
    {
        Default = SideBorderType.LegacyMaskingBorder,
        Value = SideBorderType.LegacyMaskingBorder,
    };

    [SettingSource("Display Top Bottom Borders", "Whether to display top and bottom borders in addition to side borders.")]
    public Bindable<bool> DisplayTopBottomBorders { get; private set; } = new Bindable<bool>()
    {
        Default = true,
        Value = true,
    };

    [SettingSource("Apply background dimming", "Whether to dim the background behind the playfield mask.")]
    public Bindable<bool> ApplyBackgroundDimming { get; private set; } = new Bindable<bool>()
    {
        Default = true,
        Value = true,
    };

    [Resolved]
    private IBindable<WorkingBeatmap>? beatmap { get; set; }

    [Resolved]
    private GameplayState? gameplayState { get; set; }

    [Resolved]
    private TextureStore textures { get; set; } = null!;

    private bool isWideScreenStoryboard;

    private bool shouldStoryboardVisible => showStoryboard.Value &&
        (backgroundDimLevel.Value < 1 || lightenDuringBreaks.Value);

    private Bindable<bool> showStoryboard = null!;
    private Bindable<bool> lightenDuringBreaks = null!;
    private Bindable<double> backgroundDimLevel = null!;

    private bool hasStoryboardDrawables = false;

    private readonly LayoutValue drawSizeLayout = new LayoutValue(Invalidation.DrawSize);

    public PlayfieldMask()
    {
        AddLayout(drawSizeLayout);
    }

    [Resolved]
    private GameplayClockContainer? gameplayClock { get; set; }

    private Container horizontalBorderContainer = null!;
    private Container verticalBorderContainer = null!;

    [BackgroundDependencyLoader]
    private void load(OsuConfigManager? config)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;

        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            horizontalBorderContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Colour = Colour4.Black,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Colour = Colour4.Black,
                    },
                }
            },
            verticalBorderContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
            },
        };

        if (config is not null)
        {
            lightenDuringBreaks = config.GetBindable<bool>(OsuSetting.LightenDuringBreaks);
            showStoryboard = config.GetBindable<bool>(OsuSetting.ShowStoryboard);
            backgroundDimLevel = config.GetBindable<double>(OsuSetting.DimLevel);
        }

        showStoryboard ??= new Bindable<bool>(false);
        lightenDuringBreaks ??= new Bindable<bool>(false);
        backgroundDimLevel ??= new Bindable<double>(0);

        var storyboard = gameplayState?.Storyboard ?? beatmap?.Value.Storyboard;

        if (storyboard != null)
            isWideScreenStoryboard = IsWideScreenStoryboard(storyboard);

        hasStoryboardDrawables = storyboard?.HasDrawable ?? false;

        if (gameplayClock != null)
            gameplayClock.OnSeek += gameSeeked;

        VerticalBorderType.BindValueChanged(_ => recreateSideBorderChildren(), true);
        DisplayTopBottomBorders.BindValueChanged(v =>
        {
            if (v.NewValue)
                horizontalBorderContainer.FadeIn(500);
            else
                horizontalBorderContainer.FadeOut(500);
        }, true);

        backgroundDimLevel.BindValueChanged(_ => updateBorderColour());
        ApplyBackgroundDimming.BindValueChanged(_ => updateBorderColour(), true);
    }

    private void updateBorderColour()
    {
        Colour4 borderColour = Colour4.White;

        if (ApplyBackgroundDimming.Value)
        {
            borderColour = borderColour.Darken((float)backgroundDimLevel.Value);
        }

        Colour = borderColour;
    }

    private void recreateSideBorderChildren()
    {
        verticalBorderContainer.Clear();

        bool useLegacyMaskingBorder = VerticalBorderType.Value == SideBorderType.LegacyMaskingBorder;

        verticalBorderContainer.AddRange(new Drawable[]
        {
            // left border
            new Container
            {
                RelativeSizeAxes = Axes.Y,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Colour = Colour4.Black,
                        RelativeSizeAxes = Axes.Y,
                    },
                    useLegacyMaskingBorder ? applyborderTexture(new Sprite
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        RelativeSizeAxes = Axes.Y,
                    }) : Empty(),
                }
            },
            // right border
            new Container
            {
                RelativeSizeAxes = Axes.Y,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Colour = Colour4.Black,
                        RelativeSizeAxes = Axes.Y,
                    },
                    useLegacyMaskingBorder ? applyborderTexture(new Sprite
                    {
                        Anchor = Anchor.CentreLeft,
                        // horizontally flipped, so we use CentreLeft/CentreRight
                        Origin = Anchor.CentreRight,
                        Scale = new Vector2(-1, 1),
                        RelativeSizeAxes = Axes.Y,
                    }) : Empty(),
                }
            },
        });

        Sprite applyborderTexture(Sprite sprite)
        {
            // the texture has 2 rows of identical pixels,
            // technically Repeat and ClampToEdge would look the same,
            // But somehow repeat looks better to me.
            var texture = textures.GetAutoSized("UI/masking-border", wrapModeS: WrapMode.None, wrapModeT: WrapMode.Repeat);

            sprite.Texture = texture;
            sprite.Width = texture?.DisplayWidth ?? 0;

            return sprite;
        }

        drawSizeLayout.Invalidate();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        showStoryboard.BindValueChanged(_ => scheduleFadeStateSync());
        lightenDuringBreaks.BindValueChanged(_ => scheduleFadeStateSync());
        backgroundDimLevel.BindValueChanged(_ => scheduleFadeStateSync());
        FadeOutDuringBreaks.BindValueChanged(_ => scheduleFadeStateSync(), true);

        void scheduleFadeStateSync() => Scheduler.AddOnce(syncFadeState);
    }

    public void FadeIn()
    {
        this.FadeIn(500);
    }

    public void FadeOut()
    {
        this.FadeOut(500);
    }

    private bool shouldFadeOutDuringBreaks()
    {
        return FadeOutDuringBreaks.Value switch
        {
            FadeOutBehaviour.Auto => shouldAutoFadeOutDuringBreaks(),
            FadeOutBehaviour.Always => true,
            FadeOutBehaviour.Never => false,
            _ => throw new InvalidOperationException($"Unknown FadeOutBehaviour value: {FadeOutDuringBreaks.Value}"),
        };
    }

    private bool shouldAutoFadeOutDuringBreaks()
    {
        if (hasStoryboardDrawables && shouldStoryboardVisible && !isWideScreenStoryboard)
            return false;

        // masking playfield while letterboxing is enabled in breaks looks bad,
        // because they pretty much do the same thing, just different aspect ratios,
        // but stable does it anyway, we just follow suit.
        if (beatmap?.Value.Beatmap.LetterboxInBreaks ?? false)
            return true;

        return false;
    }

    private void syncFadeState()
    {
        if (CurrentBreak.Value.HasValue && shouldFadeOutDuringBreaks())
            FadeOut();
        else
            FadeIn();
    }

    private void gameSeeked()
    {
        // Ensure correct state after a seek.
        syncFadeState();
    }

    public override void OnBreakStart()
    {
        if (shouldFadeOutDuringBreaks())
            FadeOut();
    }

    public override void OnBreakEnd()
    {
        if (shouldFadeOutDuringBreaks())
            FadeIn();
    }

    private const float target_aspect_ratio = 4f / 3f;

    protected override void Update()
    {
        base.Update();

        if (drawSizeLayout.IsValid)
            return;

        Vector2 size = DrawSize;

        float aspectRatio = size.X / size.Y;

        Vector2 centerSize;

        if (aspectRatio > target_aspect_ratio)
            centerSize = new Vector2(size.Y * target_aspect_ratio, size.Y);
        else
            centerSize = new Vector2(size.X, size.X / target_aspect_ratio);

        Vector2 maskSize = (size - centerSize) / 2;

        updateVerticalBorderWidth(maskSize.X);
        updateHorizontalBorderHeight(maskSize.Y);

        drawSizeLayout.Validate();
    }

    private void updateVerticalBorderWidth(float width)
    {
        foreach (var child in verticalBorderContainer.Children)
        {
            var container = (Container)child;

            container.Width = width;

            Debug.Assert(container.Children.Count is 2);

            var box = (Box)container.Children[0];
            var second = container.Children[1];

            box.Width = Math.Max(0, width - second.DrawWidth);
        }
    }

    private void updateHorizontalBorderHeight(float height)
    {
        foreach (var child in horizontalBorderContainer.Children)
            child.Height = height;
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (gameplayClock != null)
            gameplayClock.OnSeek -= gameSeeked;
    }

    public static bool IsWideScreenStoryboard(Storyboard storyboard)
    {
        // Wide screen storyboards use 16:9 aspect ratio while non-wide screen use 4:3.
        return storyboard.Beatmap.WidescreenStoryboard || storyboard.Layers.SelectMany(l => l.Elements).All(e => e is StoryboardVideo);
    }

    public enum FadeOutBehaviour
    {
        Auto,
        Always,
        Never,
    }

    public enum SideBorderType
    {
        BlackBar,
        LegacyMaskingBorder,
    }
}