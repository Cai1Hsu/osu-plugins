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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets;

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
    private DrawableRuleset drawableRuleset { get; set; } = null!;

    [Resolved]
    private ScoreProcessor scoreProcessor { get; set; } = null!;

    private BreakTracker breakTracker = null!;
    private readonly IBindable<bool> isBreakTime = new BindableBool();

    // use the player's clock for timing accuracy during gameplay
    public override IFrameBasedClock Clock => drawableRuleset is null ? base.Clock : drawableRuleset.FrameStableClock;

    private BreakPeriod[] breakPeriods = null!;

    private double globalPreemptTime = 0;

    [BackgroundDependencyLoader]
    private void load(IBindable<WorkingBeatmap> workingBeatmap, IBindable<IReadOnlyList<Mod>> mods, IBindable<RulesetInfo> rulesetInfo)
    {
        Debug.Assert(drawableRuleset is not null, "DrawableRuleset should be resolved when LegacyBreakOverlay is used in gameplay.");
        Debug.Assert(scoreProcessor is not null, "ScoreProcessor should be resolved when LegacyBreakOverlay is used in gameplay.");

        var beatmap = workingBeatmap.Value.Beatmap;

        globalPreemptTime = calculateGlobalPreemptTime(beatmap.BeatmapInfo, mods.Value, rulesetInfo.Value);

        breakPeriods = beatmap.Breaks
            // TODO investigate this. 
            // but in BreakTracker, only breaks with effects are considered.
            // So those without effects would never trigger our events.
            .Where(b => b.HasEffect)
            .OrderBy(b => b.StartTime)
            .ToArray();

        var firstHitObject = beatmap.HitObjects.OrderBy(h => h.StartTime).FirstOrDefault();

        if (firstHitObject is not null)
            firstHitObjectStartTime = firstHitObject.StartTime;
        else
            firstHitObjectStartTime = drawableRuleset.GameplayStartTime;

        AddInternal(breakTracker = new BreakTracker(drawableRuleset.GameplayStartTime, scoreProcessor)
        {
            Breaks = breakPeriods
        });
        isBreakTime.BindTo(breakTracker.IsBreakTime);

        if (firstHitObject is not null && firstHitObject.StartTime > 6000)
            countDownAnimationInfo = calculateCountDownArrowAnimations();
    }

    private double calculateGlobalPreemptTime(BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods, RulesetInfo rulesetInfo)
    {
        var ruleset = rulesetInfo.CreateInstance();

        // TODO: investigate if this matches stable's behavior.
        var adjustedDifficulty = ruleset.GetAdjustedDisplayDifficulty(beatmapInfo, mods);

        double ar = adjustedDifficulty.ApproachRate;

        const double min = 1800;
        const double mid = 1200;
        const double max = 450;

        return ar switch
        {
            > 5.0 => mid + (max - mid) * (ar - 5.0) / 5.0,
            < 5.0 => mid - (mid - min) * (5.0 - ar) / 5.0,
            _ => mid,
        };
    }

    private double firstHitObjectStartTime;

    private CountDownAnimationInfo? countDownAnimationInfo = null;

    private readonly struct CountDownAnimationInfo
    {
        public readonly double StartTime;
        public readonly int LoopCount;

        public CountDownAnimationInfo(double startTime, int loopCount)
        {
            StartTime = startTime;
            LoopCount = loopCount;
        }
    }

    private void scheduleCountDownAnimation()
    {
        if (countDownAnimationInfo is null)
            return;

        var info = countDownAnimationInfo.Value;

        using (BeginAbsoluteSequence(info.StartTime))
            PlayWarningAnimation(info.LoopCount);
    }

    private CountDownAnimationInfo calculateCountDownArrowAnimations()
    {
        // use integer to match stable's behavior
        int startTime = (int)firstHitObjectStartTime - (int)globalPreemptTime - 900;
        int loopCount = 5 + Math.Min(2, (int)(globalPreemptTime / 200));

        return new CountDownAnimationInfo(startTime, loopCount);
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
            else if (Clock.CurrentTime > firstHitObjectStartTime)
                ClearAnimations();
        });

        scheduleCountDownAnimation();
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
            ClearAnimations();

            if (isBreakTime.Value)
                playBreakAnimations();

            if (currentTime < firstHitObjectStartTime)
                scheduleCountDownAnimation();
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

    private const double min_break_duration_for_section_ranking = 2880;

    private void playBreakAnimations()
    {
        var maybePeriod = breakTracker.CurrentPeriod.Value;

        // if the intro is quite long, it's possible that we are in a break but no current period is set.
        if (maybePeriod is null)
            return;

        // Sometimes transparency get modified by other components, so we update it again here.
        updateLazerBreakOverlayTransparency();

        var period = maybePeriod.Value;

        // BreakTracker subtracts BreakOverlay.BREAK_FADE_DURATION from the end time to trigger the end of break earlier.
        // Using original value is confirmed to match osu!stable's behavior.
        double breakDuration = period.Duration + BreakOverlay.BREAK_FADE_DURATION;
        double breakStartTime = period.Start;
        double breakEndTime = period.End + BreakOverlay.BREAK_FADE_DURATION;

        playSectionRanking();
        playResumeWarningArrows();

        void playSectionRanking()
        {
            // match stable's behavior: only play section ranking animation for breaks longer than 2.88s
            if (breakDuration <= min_break_duration_for_section_ranking)
                return;

            double halfDuration = breakDuration / 2.0;

            double beginTime = (halfDuration > min_break_duration_for_section_ranking)
                ? (breakStartTime + halfDuration)
                : (breakEndTime - min_break_duration_for_section_ranking);

            using (BeginAbsoluteSequence(beginTime))
                PlayBreakRankingAnimation(IsSectionPassing());
        }

        void playResumeWarningArrows()
        {
            int breakIndex = getPeriodIndex(period);

            if (breakIndex == -1)
                return;

            // stable uses integer for these timings, we keep consistent.
            int preemptCount = Math.Min(2, (int)(globalPreemptTime / 200));
            int flashCount = Math.Min(5, (int)((breakDuration + 200) / 200)) + preemptCount;
            int loopStartTime = (int)(breakEndTime - 200 * (flashCount - preemptCount));

            using (BeginAbsoluteSequence(loopStartTime))
                PlayWarningAnimation(flashCount);
        }
    }
}
