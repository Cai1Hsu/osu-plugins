using System.Runtime.CompilerServices;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Plugin.LegacyErrorMeter.Tests;

public partial class TestSceneLegacyErrorMeter : OsuTestScene
{
    [Cached(typeof(ScoreProcessor))]
    private TestScoreProcessor scoreProcessor = new TestScoreProcessor();

    private HitWindows hitWindows = new DefaultHitWindows();

    private LegacyErrorMeter meter = null!;
    private double manualOffset;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("reset score processor", () => scoreProcessor.Reset());
    }

    private int overallDifficulty = 5;

    private double randomOffsetRange = 150;

    [Test]
    public void TestOsuWindows()
    {
        AddSliderStep("overall difficulty", 0, 10, 5, v => overallDifficulty = v);
        AddStep("create with OD", () => recreateMeter(new OsuHitWindows(), overallDifficulty));

        AddSliderStep("manual offset (ms)", -150, 150, 0, v => manualOffset = v);

        AddStep("apply", () => newJudgement(manualOffset));

        AddSliderStep("random offset range", 0, 150, 150, v => randomOffsetRange = v);

        AddRepeatStep("random burst", () =>
        {
            newJudgement(RNG.NextDouble(-randomOffsetRange, randomOffsetRange));
        }, 20);
        AddStep("clear meter", () => meter.Clear());
    }

    private void recreateMeter(HitWindows hitWindows, float overallDifficulty)
    {
        Clear();

        hitWindows.SetDifficulty(overallDifficulty);
        this.hitWindows = hitWindows;

        AddRange(new Drawable[]
        {
            new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(0, 4),
                Position = new Vector2(0, -120),
                ChildrenEnumerable = hitWindows.GetAllAvailableWindows().Select(window =>
                new OsuSpriteText
                {
                    Text = $"{window.result}: {window.length:0.##}"
                }) ?? Enumerable.Empty<Drawable>()
            },
            meter = new LegacyErrorMeter
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(1.1f)
            }.With(m => m.HideBeforeFirstHit.Value = false) // for testing purposes
        });

        meter.OnLoadComplete += _ => meter.MeterDrawable.SetHitWindows(hitWindows);
    }

    private void newJudgement(double offset = 0)
    {
        var windows = hitWindows ?? HitWindows.Empty;

        var hitObject = new HitCircle { HitWindows = windows };
        var judgement = new JudgementResult(hitObject, new Judgement())
        {
            Type = windows == HitWindows.Empty ? HitResult.Great : windows.ResultFor(offset)
        };
        set_TimeOffset(judgement, offset);

        scoreProcessor.ApplyResult(judgement);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_TimeOffset")]
        static extern void set_TimeOffset(JudgementResult _, double value);
    }

    private partial class TestScoreProcessor : ScoreProcessor
    {
        public TestScoreProcessor()
            : base(new OsuRuleset())
        {
        }

        public void Reset() => base.Reset(false);
    }
}
