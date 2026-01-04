using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Objects;
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
    public Bindable<float> LazerBreakOverlayTransparency { get; } = new BindableFloat(1)
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

    private BreakPeriod[] breakPeriods = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        breakPeriods = beatmap.Value.Beatmap.Breaks
            // TODO investigate this. 
            // but in BreakTracker, only breaks with effects are considered.
            // So those without effects would never trigger our events.
            .Where(b => b.HasEffect)
            .OrderBy(b => b.StartTime)
            .ToArray();

        preemptTimesForBreaks = calculatePreemptTimeForBreaks();

        Debug.Assert(drawableRuleset is not null, "DrawableRuleset should be resolved when LegacyBreakOverlay is used in gameplay.");
        Debug.Assert(scoreProcessor is not null, "ScoreProcessor should be resolved when LegacyBreakOverlay is used in gameplay.");

        AddInternal(breakTracker = new BreakTracker(drawableRuleset.GameplayStartTime, scoreProcessor)
        {
            Breaks = breakPeriods
        });
        isBreakTime.BindTo(breakTracker.IsBreakTime);
    }

    private double[] preemptTimesForBreaks = null!;

    private double[] calculatePreemptTimeForBreaks()
    {
        var beatmap = this.beatmap.Value.Beatmap;
        var breaks = breakPeriods;

        if (breaks.Length == 0)
            return Array.Empty<double>();

        // osu didn't gurantee that hitobjects are sorted, so we sort them first.
        // I don't know if this would be a performance concern or not.
        // In my tests, a beatmap(b/349685) with 10k hitobjects takes about 1ms to sort on my machine.
        // This is the beatmap with most hitobjects on my machine, so I guess it's acceptable.
        var hitObjects = beatmap.HitObjects.OrderBy(h => h.StartTime).ToArray();
        double[] preemptTimes = new double[breaks.Length];

        for (int i = 0; i < preemptTimes.Length; i++)
            preemptTimes[i] = double.NaN;

        for (int breakIndex = 0; breakIndex < breaks.Length; breakIndex++)
        {
            var breakPeriod = breaks[breakIndex];
            var nextHitObject = binarySearchFirstHitObjectAfter(hitObjects, breakPeriod.EndTime);

            if (nextHitObject is null)
                continue;

            double preemptTime;
            if (nextHitObject is IHasTimePreempt hasTimePreempt)
                preemptTime = hasTimePreempt.TimePreempt;
            else
                // FIXME
                // I don't know what does this mean
                // This line was from: https://github.com/ppy/osu/blob/e0c4592dc74a69aff1453a8e19e3ec0f5e8f2ca9/osu.Game/Screens/Edit/EditorBeatmapProcessor.cs#L88-L91
                // i see this value is used when the next object is not IHasTimePreempt, so i guess it's a fallback value.
                preemptTime = Math.Max(BreakPeriod.GAP_AFTER_BREAK, beatmap.ControlPointInfo.TimingPointAt(nextHitObject.StartTime).BeatLength * 2);

            preemptTimes[breakIndex] = preemptTime;
        }

        return preemptTimes;
    }

    private static HitObject? binarySearchFirstHitObjectAfter(IReadOnlyList<HitObject> hitObjects, double time)
    {
        if (hitObjects.Count == 0)
            return null;

        int low = 0;
        int high = hitObjects.Count - 1;
        HitObject? candidate = null;

        while (low <= high)
        {
            int mid = (low + high) >> 1;
            var hitObject = hitObjects[mid];

            if (hitObject.StartTime >= time)
            {
                candidate = hitObject;
                high = mid - 1;
            }
            else
                low = mid + 1;
        }

        return candidate;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        LazerBreakOverlayTransparency.BindValueChanged(v =>
        {
            updateLazerBreakOverlayTransparency();
        }, true);

        isBreakTime.BindValueChanged(v =>
        {
            if (v.NewValue)
                playBreakAnimations();
            else
                ClearAnimations();
        });
    }

    private void updateLazerBreakOverlayTransparency()
    {
        if (player?.BreakOverlay is null)
            return;

        player.BreakOverlay.Alpha = LazerBreakOverlayTransparency.Value;
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
        var breaks = breakPeriods;

        for (int i = 0; i < breaks.Length; i++)
        {
            if (Precision.AlmostEquals(period.Start, breaks[i].StartTime) &&
                // BreakTracker adjusts the end time by subtracting BreakOverlay.BREAK_FADE_DURATION
                Precision.AlmostEquals(period.End, breaks[i].EndTime - BreakOverlay.BREAK_FADE_DURATION))
                return i;
        }

        return -1;
    }

    private void playBreakAnimations()
    {
        // Sometimes transparency get modified by other components, so we update it again here.
        updateLazerBreakOverlayTransparency();

        var maybePeriod = breakTracker.CurrentPeriod.Value;

        Debug.Assert(maybePeriod.HasValue, "Current break period should have value when in break time.");

        var period = maybePeriod.Value;

        playSectionRanking();
        playResumeWarningArrows();

        void playSectionRanking()
        {
            double halfDuration = period.Duration / 2.0;

            double gameStartTime = drawableRuleset?.GameplayStartTime ?? 0;
            double playTime = (halfDuration > 2880) ? (period.Start + halfDuration) : (period.End - 2880);

            double beginTime = Math.Max(0, playTime - gameStartTime);

            // I see sections ranking animation won't play if the break time is too short.
            // But this hack also skips animation when rewinding past the animation time.
            // though this is not quite common and the effect is minor.
            if (beginTime < Clock.CurrentTime)
                return;

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
