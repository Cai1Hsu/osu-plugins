using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;

namespace osu.Plugin.LegacyBreakOverlay;

public partial class LegacyBreakOverlay : CompositeDrawable, ISerialisableDrawable
{
    bool ISerialisableDrawable.UsesFixedAnchor { get; set; } = true;

    [SettingSource("Disable lazer break overlay", "Disables the built-in lazer break overlay.")]
    public BindableBool DisableLazerBreakOverlay { get; } = new BindableBool(false);

    [Resolved]
    private Player? player { get; set; }

    [Resolved]
    private DrawableRuleset? drawableRuleset { get; set; }

    [Resolved]
    private ScoreProcessor? scoreProcessor { get; set; }

    [Resolved]
    private ISkinSource? skin { get; set; } = null;

    [Resolved]
    private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

    private BreakTracker? breakTracker;
    private readonly IBindable<bool> isBreakTime = new BindableBool();

    private Container warningContainer = null!;
    private Sprite sectionResultSprite;

    private static readonly Vector2 warning_arrow_position = new Vector2(80, 100);
    private const float warning_arrow_duration = 100;
    public override IFrameBasedClock Clock => drawableRuleset is null ? base.Clock : drawableRuleset.FrameStableClock;

    private BreakTracker? createBreakTracker() => drawableRuleset is null || scoreProcessor is null ? null : new BreakTracker(drawableRuleset.GameplayStartTime, scoreProcessor)
    {
        Breaks = beatmap.Value.Beatmap.Breaks
    };

    public LegacyBreakOverlay()
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Position = Vector2.Zero;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            sectionResultSprite = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            },
            warningContainer = new Container()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new WarningArrow
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.Centre,
                        Position = warning_arrow_position,
                    },
                    new WarningArrow
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.Centre,
                        Position = new Vector2(warning_arrow_position.X, -warning_arrow_position.Y),
                    },
                    new WarningArrow
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-warning_arrow_position.X, warning_arrow_position.Y),
                        Scale = new Vector2(-1, 1),
                    },
                    new WarningArrow
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-warning_arrow_position.X, -warning_arrow_position.Y),
                        Scale = new Vector2(-1, 1),
                    }
                },
            }
        };
    }

    private PoolableSkinnableSample? sectionPassSample = null;
    private PoolableSkinnableSample? sectionFailSample = null;

    [BackgroundDependencyLoader]
    private void load()
    {
        // FIXME: investigate this, samples from custom skins are not applying correctly.
        AddInternal(sectionPassSample = new PoolableSkinnableSample(new SampleInfo("Gameplay/sectionpass")));
        AddInternal(sectionFailSample = new PoolableSkinnableSample(new SampleInfo("Gameplay/sectionfail")));

        preemptTimesForBreaks = calculatePreemptTimeForBreaks();
    }

    private double[] preemptTimesForBreaks = null!;

    private double[] calculatePreemptTimeForBreaks()
    {
        var beatmap = this.beatmap.Value.Beatmap;
        var breaks = beatmap.Breaks;

        if (breaks.Count == 0)
            return Array.Empty<double>();

        var hitObjects = beatmap.HitObjects;
        double[] preemptTimes = new double[breaks.Count];

        for (int i = 0; i < preemptTimes.Length; i++)
            preemptTimes[i] = double.NaN;

        var currentBreak = 0;

        for (int i = 0; i < hitObjects.Count; i++)
        {
            var hitObject = hitObjects[i];

            if (hitObject.StartTime < breaks[currentBreak].StartTime)
                continue;

            double preemptTime;
            if (hitObject is IHasTimePreempt hasTimePreempt)
                preemptTime = hasTimePreempt.TimePreempt;
            else
                // FIXME
                // I don't know what does this mean
                // This line was from: https://github.com/ppy/osu/blob/e0c4592dc74a69aff1453a8e19e3ec0f5e8f2ca9/osu.Game/Screens/Edit/EditorBeatmapProcessor.cs#L88-L91
                // i see this value is used when the next object is not IHasTimePreempt, so i guess it's a fallback value.
                preemptTime = Math.Max(BreakPeriod.GAP_AFTER_BREAK, beatmap.ControlPointInfo.TimingPointAt(hitObject.StartTime).BeatLength * 2);

            preemptTimes[currentBreak] = preemptTime;

            if (++currentBreak >= breaks.Count)
                break;
        }

        return preemptTimes;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        DisableLazerBreakOverlay.BindValueChanged(v =>
        {
            var defaultBreakOverlay = player?.BreakOverlay;

            if (defaultBreakOverlay == null)
                return;

            if (v.NewValue)
                defaultBreakOverlay.Hide();
            else
                defaultBreakOverlay.Show();
        }, true);

        breakTracker = createBreakTracker();

        if (breakTracker is not null)
        {
            AddInternal(breakTracker);

            isBreakTime.BindTo(breakTracker.IsBreakTime);
        }

        isBreakTime.BindValueChanged(v =>
        {
            if (v.NewValue)
                playBreakAnimations();
            else
                ClearAllAnimations();
        });

        ClearAllAnimations();
    }

    private double lastFrameTime = double.MinValue;
    protected override void Update()
    {
        base.Update();

        double currentTime = Clock.CurrentTime;

        // When rewinding, we need to re-play the break animations if we're currently in a break.
        if (currentTime < lastFrameTime)
        {
            if (breakTracker is not null)
            {
                if (isBreakTime.Value)
                    playBreakAnimations();
                else
                    ClearAllAnimations();
            }
        }

        lastFrameTime = currentTime;
    }

    private bool isSectionPassing()
    {
        // lazer didn't provide sections passing/failing feedback, so we approximate it here.
        if (scoreProcessor is null || breakTracker is null)
            return true;

        // This should be a reasonable approximation of passing a section.
        return scoreProcessor.Accuracy.Value > 0.9;
    }

    private int getPeriodIndex(Period period)
    {
        var breaks = beatmap.Value.Beatmap.Breaks;

        for (int i = 0; i < breaks.Count; i++)
        {
            if (Precision.AlmostEquals(period.Start, breaks[i].StartTime) &&
                Precision.AlmostEquals(period.End, breaks[i].EndTime - BreakOverlay.BREAK_FADE_DURATION))
                return i;
        }

        return -1;
    }

    private void playBreakAnimations()
    {
        Debug.Assert(breakTracker is not null);

        var maybePeriod = breakTracker.CurrentPeriod.Value;

        if (maybePeriod is null)
            return;

        var period = maybePeriod.Value;

        playSectionRanking();
        playResumeWarningArrows();

        void playSectionRanking()
        {
            double halfDuration = period.Duration / 2.0;

            double gameStartTime = drawableRuleset?.GameplayStartTime ?? 0;
            double playTime = (halfDuration > 2880) ? (period.Start + halfDuration) : (period.End - 2880);

            double beginTime = Math.Max(0, playTime - gameStartTime);

            using (BeginAbsoluteSequence(beginTime))
                PlayBreakRankingAnimation(isSectionPassing());
        }

        void playResumeWarningArrows()
        {
            int breakIndex = getPeriodIndex(period);

            if (breakIndex == -1)
                return;

            double preemptTime = preemptTimesForBreaks[breakIndex];

            if (double.IsNaN(preemptTime))
                return;

            // stable uses int for these timings, we keep consistent.
            int preemptCount = Math.Min(2, (int)(preemptTime / 200));
            int flashCount = Math.Min(5, (int)((period.Duration + 200) / 200)) + preemptCount;
            int loopStartTime = (int)(period.End - 200 * (flashCount - preemptCount));

            using (BeginAbsoluteSequence(loopStartTime))
                PlayWarningAnimation(flashCount);
        }
    }

    public void PlayWarningAnimation(int loopCount)
    {
        if (loopCount <= 0)
            return;

        var transform = warningContainer.FadeIn()
            .Delay(warning_arrow_duration)
            .FadeOut();

        if (loopCount > 1)
            transform.Loop(warning_arrow_duration, loopCount);
    }

    public void ClearWarningAnimation()
    {
        warningContainer.ClearTransforms();
        warningContainer.FadeOut();
    }

    public void PlayBreakRankingAnimation(bool passing)
    {
        ClearBreakRankingAnimation();

        scheduledSamplePlay = Schedule(() =>
        {
            playSample();
            scheduledSamplePlay = null;
        });

        Texture? texture = skin?.GetTexture(passing ? "section-pass" : "section-fail");

        if (texture is null)
            return;

        sectionResultSprite.Texture = texture;

        playAnimation();

        void playSample()
        {
            if (passing)
                sectionPassSample?.Play();
            else
                sectionFailSample?.Play();
        }

        void playAnimation()
        {
            if (passing)
                playPassingAnimation();
            else
                playFailingAnimation();
        }
    }

    private ScheduledDelegate? scheduledSamplePlay;

    public void ClearAllAnimations()
    {
        ClearWarningAnimation();
        ClearBreakRankingAnimation();

        scheduledSamplePlay?.Cancel();
        scheduledSamplePlay = null;
    }

    public void ClearBreakRankingAnimation()
    {
        sectionResultSprite.ClearTransforms();
        sectionResultSprite.FadeOut();
    }

    private void playPassingAnimation()
    {
        sectionResultSprite
            .Delay(20)
            .FadeInFromZero()
            .Delay(80)
            .FadeOutFromOne()
            .Delay(60)
            .FadeInFromZero()
            .Delay(70)
            .FadeOutFromOne()
            .Delay(50)
            .FadeInFromZero()
            .Delay(1000)
            .FadeOutFromOne(200);
    }

    private void playFailingAnimation()
    {
        sectionResultSprite
            .Delay(130)
            .FadeInFromZero()
            .Delay(100)
            .FadeOutFromOne()
            .Delay(50)
            .FadeInFromZero()
            .Delay(1000)
            .FadeOutFromOne(200);
    }

    private partial class WarningArrow : Sprite
    {
        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            Texture? texture = skin.GetTexture("arrow-warning")
                ?? skin.GetTexture("arrow-pause");

            if (texture is not null)
                Texture = texture;
        }
    }
}
