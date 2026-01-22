using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
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

    private Box topMask = null!;
    private Box bottomMask = null!;
    private Box leftMask = null!;
    private Box rightMask = null!;

    [Resolved]
    private IBindable<WorkingBeatmap>? beatmap { get; set; }

    [Resolved]
    private GameplayState? gameplayState { get; set; }

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

    [BackgroundDependencyLoader]
    private void load(OsuConfigManager? config)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;

        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            topMask = new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Colour = Colour4.Black,
            },
            bottomMask = new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Colour = Colour4.Black,
            },
            leftMask = new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Colour = Colour4.Black,
            },
            rightMask = new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Colour = Colour4.Black,
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
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        FadeOutDuringBreaks.BindValueChanged(_ => syncFadeState(), true);
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
        // because they pretty much does the same thing, just different aspect ratios,
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

        topMask.Size = new Vector2(size.X, maskSize.Y);
        bottomMask.Size = new Vector2(size.X, maskSize.Y);

        leftMask.Size = new Vector2(maskSize.X, centerSize.Y);
        rightMask.Size = new Vector2(maskSize.X, centerSize.Y);

        drawSizeLayout.Validate();
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
}
