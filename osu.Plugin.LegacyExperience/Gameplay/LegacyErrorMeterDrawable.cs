using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacyErrorMeterDrawable : CompositeDrawable
{
    private const float stable_ratio = 1.6f;

    private const float bar_height = 3f * stable_ratio;
    private const float background_height = bar_height * 4f;
    private const float centre_line_width = 1.5f * stable_ratio;
    private const double arrow_move_duration = 800;

    private Container segmentsContainer = null!;
    private LegacyJudgements judgements = null!;
    private ArrowAverageIndicator arrow = null!;

    private double errorRange = 1;
    private double? floatingAverage;

    [Resolved]
    private OsuColour osuColour { get; set; } = null!;

    public LegacyErrorMeterDrawable()
    {
        Height = background_height;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Name = "background",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Colour4.Black.Opacity(0.6f),
                        RelativeSizeAxes = Axes.Both
                    },
                    segmentsContainer = new Container
                    {
                        Name = "segments",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Height = 0.25f, // quarter height of the background
                    },
                    judgements = new LegacyJudgements()
                    {
                        Name = "judgements",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        SparkRelativeSizeAxes = Axes.Y,
                        SparkSize = new Vector2(centre_line_width, 1),
                    },
                    new Box
                    {
                        Name = "centre line",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Colour4.White,
                        Width = centre_line_width,
                        RelativeSizeAxes = Axes.Y
                    }
                }
            },
            arrow = new ArrowAverageIndicator()
            {
                Name = "average arrow",
                Anchor = Anchor.TopCentre,
                // The arrow is scaled upside down
                // so bottom center is the visual top center.
                Origin = Anchor.BottomCentre,
            },
        };
    }

    public void SetHitWindows(HitWindows? hitWindows)
    {
        if (hitWindows == null)
            return;

        var availableWindows = hitWindows.GetAllAvailableWindows()
            .Where(w => w.result.IsHit())
            .OrderByDescending(w => w.length)
            .ToArray();

        if (availableWindows.Length == 0)
            return;

        errorRange = availableWindows.First().length;

        if (errorRange <= 0)
            return;

        Width = (float)(errorRange * stable_ratio);

        segmentsContainer.Clear();

        foreach (var window in availableWindows)
        {
            var width = (float)(Math.Clamp(window.length, 0, errorRange) / errorRange) * Width;

            segmentsContainer.Add(new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Y,
                Width = width,
                Colour = getColour(window.result)
            });
        }

        ClearJudgements();
    }

    public void ProcessJudgement(HitResult result, double timeOffset)
    {
        if (!result.IsHit() || result.IsBonus())
            return;

        // stable uses int for time offset
        int stableTimeOffset = (int)Math.Round(timeOffset);

        double clamped = Math.Clamp(stableTimeOffset, -errorRange, errorRange);
        float offset = (float)(clamped / errorRange) * (Width / 2f);

        if (floatingAverage == null)
            floatingAverage = offset;
        else
            floatingAverage = floatingAverage * 0.8 + offset * 0.2;

        arrow.MoveToX((float)floatingAverage, arrow_move_duration, Easing.Out);

        spawnSpark(getColour(result), offset);
    }

    public void ClearJudgements()
    {
        floatingAverage = null;

        arrow.MoveToX(0, 200, Easing.Out);

        judgements.Clear();
    }

    private void spawnSpark(Colour4 colour, float offsetPixels)
    {
        var judgementsLocal = new Vector2(offsetPixels, 0) + judgements.OriginPosition;

        judgements.SpawnSpark(judgementsLocal, colour);
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

    private partial class ArrowAverageIndicator : Triangle
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Size = new Vector2(10.625f, 5f);
            Scale = new Vector2(1, -1); // make it upside down
            Colour = Colour4.White;
        }
    }
}
