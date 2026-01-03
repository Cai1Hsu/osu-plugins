using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Plugin.LegacyBreakOverlay;

/// <summary>
/// The skin component that provides full legacy break overlay experience.
/// </summary>
public partial class LegacyBreakOverlay : LegacyBreakOverlayBase, ISerialisableDrawable
{
    bool ISerialisableDrawable.UsesFixedAnchor { get; set; } = true;

    [SettingSource("lazer break overlay transparency", "Set the transparency of the lazer's built-in break overlay.")]
    public BindableFloat LazerBreakOverlayTransparency { get; } = new BindableFloat(1)
    {
        MinValue = 0,
        MaxValue = 1,
        Default = 1,
        Precision = 0.01f,
    };

    [Resolved]
    private Player? player { get; set; }

    [Resolved]
    private DrawableRuleset? drawableRuleset { get; set; }

    [Resolved]
    private ScoreProcessor? scoreProcessor { get; set; }

    [Resolved]
    private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

    private BreakTracker breakTracker = null!;
    private readonly IBindable<bool> isBreakTime = new BindableBool();

    // use the player's clock for timing accuracy during gameplay
    public override IFrameBasedClock Clock => drawableRuleset is null ? base.Clock : drawableRuleset.FrameStableClock;

    private BreakTracker? createBreakTracker() => drawableRuleset is null || scoreProcessor is null ? null : new BreakTracker(drawableRuleset.GameplayStartTime, scoreProcessor)
    {
        Breaks = beatmap.Value.Beatmap.Breaks
    };

    [BackgroundDependencyLoader]
    private void load()
    {
        preemptTimesForBreaks = calculatePreemptTimeForBreaks();

        breakTracker = createBreakTracker()!;

        Debug.Assert(breakTracker is not null, "BreakTracker should not be null when created here.");

        AddInternal(breakTracker);
        isBreakTime.BindTo(breakTracker.IsBreakTime);
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

        LazerBreakOverlayTransparency.BindValueChanged(v =>
        {
            var defaultBreakOverlay = player?.BreakOverlay;

            if (defaultBreakOverlay == null)
                return;

            defaultBreakOverlay.Alpha = v.NewValue;
        }, true);

        isBreakTime.BindValueChanged(v =>
        {
            if (v.NewValue)
                playBreakAnimations();
            else
                ClearAnimations();
        }, true);
    }

    private double lastFrameTime = double.MinValue;
    protected override void Update()
    {
        base.Update();

        double currentTime = Clock.CurrentTime;

        // When rewinding, we need to re-play the break animations if we're currently in a break.
        if (currentTime < lastFrameTime)
        {
            if (isBreakTime.Value)
                playBreakAnimations();
            else
                ClearAnimations();
        }

        lastFrameTime = currentTime;
    }

    protected virtual bool IsSectionPassing()
    {
        // lazer didn't provide sections passing/failing feedback, so we approximate it here.
        if (scoreProcessor is null)
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
        var maybePeriod = breakTracker.CurrentPeriod.Value;

        Debug.Assert(maybePeriod.HasValue);

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
                PlayBreakRankingAnimation(IsSectionPassing());
        }

        void playResumeWarningArrows()
        {
            int breakIndex = getPeriodIndex(period);

            if (breakIndex == -1)
                return;

            double preemptTime = preemptTimesForBreaks[breakIndex];

            if (double.IsNaN(preemptTime))
                return;

            // stable uses integer for these timings, we keep consistent.
            int preemptCount = Math.Min(2, (int)(preemptTime / 200));
            int flashCount = Math.Min(5, (int)((period.Duration + 200) / 200)) + preemptCount;
            int loopStartTime = (int)(period.End - 200 * (flashCount - preemptCount));

            using (BeginAbsoluteSequence(loopStartTime))
                PlayWarningAnimation(flashCount);
        }
    }
}
