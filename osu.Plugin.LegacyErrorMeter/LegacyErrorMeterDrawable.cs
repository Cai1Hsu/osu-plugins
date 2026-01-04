using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Plugin.LegacyErrorMeter;

public partial class LegacyErrorMeterDrawable : CompositeDrawable
{
    private const float bar_height = 12f;
    private const float background_height = bar_height * 4f;
    private const float centre_line_width = 1.5f; // TODO: this looks in correct
    private const float min_meter_width = 220f;
    private const float max_meter_width = 420f;
    private const float pixels_per_millisecond = 2.2f;
    private const double arrow_move_duration = 800;

    private readonly DrawablePool<LegacyJudgementSpark> sparkPool = new DrawablePool<LegacyJudgementSpark>(64);
    private readonly List<Box> segments = new List<Box>();

    private Container surface = null!;
    private Container segmentsContainer = null!;
    private Container flashContainer = null!;
    private Box background = null!;
    private Box centreLine = null!;
    private Container arrowContainer = null!;
    private SpriteIcon arrow = null!; // FIXME: draw one ourselves

    private (HitResult result, double length)[] availableWindows = Array.Empty<(HitResult, double)>();
    private HitWindows? sourceHitWindows;

    private float meterWidth = min_meter_width;
    private double errorRange = 1;
    private double floatingAverage;

    [Resolved]
    private OsuColour osuColour { get; set; } = null!;

    public LegacyErrorMeterDrawable()
    {
        AutoSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChildren = new Drawable[]
        {
            sparkPool,
            surface = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        Name = "background",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Colour4.Black.Opacity(0.6f),
                        Size = new Vector2(meterWidth, background_height)
                    },
                    segmentsContainer = new Container
                    {
                        Name = "segments",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both
                    },
                    flashContainer = new Container
                    {
                        Name = "flashes",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both
                    },
                    centreLine = new Box
                    {
                        Name = "centre line",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Colour4.White,
                        Width = centre_line_width,
                        Height = background_height
                    }
                }
            },
            arrowContainer = new Container
            {
                Name = "average arrow",
                Anchor = Anchor.Centre,
                Origin = Anchor.BottomCentre,
                AutoSizeAxes = Axes.Both,
                Scale = new Vector2(1, -1),
                Y = -(background_height / 2f + 6f),
                Alpha = 0,
                Child = arrow = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = FontAwesome.Solid.ChevronRight,
                    Rotation = -90,
                    Size = new Vector2(14),
                    Colour = Colour4.White
                }
            }
        };
    }

    public void SetHitWindows(HitWindows? hitWindows)
    {
        if (hitWindows == null || ReferenceEquals(sourceHitWindows, hitWindows))
            return;

        sourceHitWindows = hitWindows;
        availableWindows = hitWindows.GetAllAvailableWindows().Where(w => w.result.IsHit()).OrderByDescending(w => w.length).ToArray();

        errorRange = Math.Max(1, availableWindows.Length > 0 ? availableWindows.First().length : 1);
        meterWidth = (float)Math.Clamp(errorRange * pixels_per_millisecond, min_meter_width, max_meter_width);

        background.Size = new Vector2(meterWidth, background_height);
        centreLine.Height = background_height;

        ensureSegments(availableWindows.Length);

        for (int i = 0; i < availableWindows.Length; i++)
        {
            var window = availableWindows[i];
            var width = (float)(Math.Clamp(window.length, 0, errorRange) / errorRange) * meterWidth;
            segments[i].Width = width;
            segments[i].Height = bar_height;
            segments[i].Alpha = 1;
            segmentsContainer.ChangeChildDepth(segments[i], 1 - i / (float)availableWindows.Length);
        }

        for (int i = availableWindows.Length; i < segments.Count; i++)
            segments[i].Alpha = 0;

        arrowContainer.Position = new Vector2(0, -(background_height / 2f + 6f));
        floatingAverage = 0;
        updateSegmentColours();
    }

    public void ProcessJudgement(HitResult result, double timeOffset)
    {
        if (!result.IsHit() || result.IsBonus())
            return;

        if (availableWindows.Length == 0)
            return;

        double clamped = Math.Clamp(timeOffset, -errorRange, errorRange);
        float offsetPixels = (float)(clamped / errorRange) * (meterWidth / 2f);

        floatingAverage = floatingAverage * 0.8 + offsetPixels * 0.2;

        arrowContainer
            .FadeIn(250, Easing.OutQuint)
            .MoveToX((float)floatingAverage, arrow_move_duration, Easing.Out);

        spawnSpark(Math.Abs(clamped), offsetPixels);
    }

    public void ClearJudgements()
    {
        floatingAverage = 0;

        arrowContainer
            .FadeOut(200, Easing.OutQuint)
            .MoveToX(0, 200, Easing.OutQuint);

        foreach (var drawable in flashContainer.Children.ToArray())
        {
            drawable.ClearTransforms();
            drawable.Expire();
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        arrowContainer.Alpha = 0;
    }

    private void spawnSpark(double absoluteOffset, float offsetPixels)
    {
        var spark = sparkPool.Get();
        spark.Apply(determineColourForOffset(absoluteOffset), offsetPixels, background_height);
        flashContainer.Add(spark);
    }

    private void ensureSegments(int count)
    {
        while (segments.Count < count)
        {
            var box = new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Height = bar_height,
                Alpha = 0
            };

            segments.Add(box);
            segmentsContainer.Add(box);
        }
    }

    private void updateSegmentColours()
    {
        for (int i = 0; i < availableWindows.Length && i < segments.Count; i++)
            segments[i].Colour = getColour(availableWindows[i].result);
    }

    private Colour4 determineColourForOffset(double absoluteOffset)
    {
        if (availableWindows.Length == 0)
            return getColour(HitResult.Great);

        foreach (var window in availableWindows.OrderBy(w => w.length))
        {
            if (absoluteOffset <= window.length + 0.01)
                return getColour(window.result);
        }

        return getColour(availableWindows.Last().result);
    }

    private Colour4 getColour(HitResult result) => result switch
    {
        HitResult.Perfect or
        HitResult.Great => new Colour4(50, 188, 231, 255),
        HitResult.Ok or
        HitResult.Good => new Colour4(87, 227, 19, 255),
        HitResult.Meh => new Colour4(218, 174, 70, 255),
        HitResult.Miss => Colour4.Red,
        _ => osuColour.ForHitResult(result)
    };

    private partial class LegacyJudgementSpark : PoolableDrawable
    {
        private readonly Box box;

        public LegacyJudgementSpark()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Width = 1.5f;
            Scale = new Vector2(3, 1);
            Height = background_height;

            Blending = BlendingParameters.Additive;

            AddInternal(box = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            });
        }

        public void Apply(Colour4 colour, float offset, float height)
        {
            ClearTransforms();

            Alpha = 1;
            Position = new Vector2(offset, 0);
            Height = height;
            box.Colour = colour;

            this
                .FadeOut(10000, Easing.OutCubic)
                .Expire();
        }
    }
}
