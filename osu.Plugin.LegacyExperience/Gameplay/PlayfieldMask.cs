using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Layout;
using osu.Game.Configuration;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
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

    [SettingSource("Fade out during breaks", "Whether the playfield masks should fade out during breaks.")]
    public Bindable<bool> FadeOutDuringBreaks { get; private set; } = new Bindable<bool>(true);

    private Box topMask = null!;
    private Box bottomMask = null!;
    private Box leftMask = null!;
    private Box rightMask = null!;

    private readonly LayoutValue drawSizeLayout = new LayoutValue(Invalidation.DrawSize);

    public PlayfieldMask()
    {
        AddLayout(drawSizeLayout);
    }

    [Resolved]
    private GameplayClockContainer? gameplayClock { get; set; }

    [BackgroundDependencyLoader]
    private void load()
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

    private void syncFadeState()
    {
        if (FadeOutDuringBreaks.Value && CurrentBreak.Value.HasValue)
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
        if (FadeOutDuringBreaks.Value)
            FadeOut();
    }

    public override void OnBreakEnd()
    {
        if (FadeOutDuringBreaks.Value)
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
}
