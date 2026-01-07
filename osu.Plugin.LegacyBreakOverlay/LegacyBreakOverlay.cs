using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets;
using osu.Game.Plugins.Legacy;
using osu.Framework.Graphics;
using osu.Game.Beatmaps.Timing;

namespace osu.Plugin.LegacyBreakOverlay;

/// <summary>
/// The skin component that provides full legacy break overlay experience.
/// </summary>
public partial class LegacyBreakOverlay : BreakTrackingContainer, ISerialisableDrawable
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
    private ScoreProcessor scoreProcessor { get; set; } = null!;

    private double globalPreemptTime = 0;

    private LegacyBreakOverlayDrawable overlay = null!;

    public LegacyBreakOverlay()
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
    }

    [BackgroundDependencyLoader]
    private void load(IBindable<WorkingBeatmap> workingBeatmap, IBindable<IReadOnlyList<Mod>> mods, IBindable<RulesetInfo> rulesetInfo, IGameplayClock gameplayClock)
    {
        Debug.Assert(scoreProcessor is not null, "ScoreProcessor should be resolved when LegacyBreakOverlay is used in gameplay.");

        Add(overlay = new LegacyBreakOverlayDrawable());

        var beatmap = workingBeatmap.Value.Beatmap;

        globalPreemptTime = calculateGlobalPreemptTime(beatmap.BeatmapInfo, mods.Value, rulesetInfo.Value);

        var firstHitObject = beatmap.HitObjects.OrderBy(h => h.StartTime).FirstOrDefault();

        double firstHitObjectStartTime;

        if (firstHitObject is not null)
            firstHitObjectStartTime = firstHitObject.StartTime;
        else
            firstHitObjectStartTime = gameplayClock.GameplayStartTime;

        if (firstHitObject is not null && firstHitObject.StartTime > 6000)
            scheduleCountDownAnimation(firstHitObjectStartTime);

        // schedule all animations to avoid conflicts when rewinding.
        foreach (var period in beatmap.Breaks)
            scheduleAnimationForBreak(period);
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

    private void scheduleCountDownAnimation(double firstHitObjectStartTime)
    {
        // use integer to match stable's behavior
        int startTime = (int)firstHitObjectStartTime - (int)globalPreemptTime - 900;
        int loopCount = 5 + Math.Min(2, (int)(globalPreemptTime / 200));

        using (BeginAbsoluteSequence(startTime))
            overlay.PlayWarningAnimation(loopCount);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        LazerBreakOverlayTransparency.BindValueChanged(v =>
        {
            updateLazerBreakOverlayTransparency();
        }, true);
    }

    private void updateLazerBreakOverlayTransparency()
    {
        if (player?.BreakOverlay is null)
            return;

        player.BreakOverlay.Alpha = LazerBreakOverlayTransparency.Value;
    }

    protected virtual bool IsSectionPassing()
    {
        // lazer didn't provide sections passing/failing feedback, so we approximate it here.
        if (scoreProcessor is null)
            return true;

        // This should be a reasonable approximation of passing a section.
        return scoreProcessor.Accuracy.Value > 0.9;
    }

    private const double min_break_duration_for_section_ranking = 2880;

    private void scheduleAnimationForBreak(BreakPeriod period)
    {
        // Sometimes transparency get modified by other components, so we update it again here.
        updateLazerBreakOverlayTransparency();

        // BreakTracker subtracts BreakOverlay.BREAK_FADE_DURATION from the end time to trigger the end of break earlier.
        // Using original value is confirmed to match osu!stable's behavior.
        double breakDuration = period.Duration;
        double breakStartTime = period.StartTime;
        double breakEndTime = period.EndTime;

        using (BeginAbsoluteSequence(breakStartTime))
        {
            playSectionRanking();
            playResumeWarningArrows();
        }

        void playSectionRanking()
        {
            // match stable's behavior: only play section ranking animation for breaks longer than 2.88s
            if (breakDuration <= min_break_duration_for_section_ranking)
                return;

            double halfDuration = breakDuration / 2.0;

            double beginTime = (halfDuration > min_break_duration_for_section_ranking)
                ? (breakStartTime + halfDuration)
                : (breakEndTime - min_break_duration_for_section_ranking);

            using (BeginDelayedSequence(beginTime - breakStartTime))
                overlay.PlayBreakRankingAnimation(IsSectionPassing());
        }

        void playResumeWarningArrows()
        {
            // stable uses integer for these timings, we keep consistent.
            int preemptCount = Math.Min(2, (int)(globalPreemptTime / 200));
            int flashCount = Math.Min(5, (int)((breakDuration + 200) / 200)) + preemptCount;
            int loopStartTime = (int)(breakEndTime - 200 * (flashCount - preemptCount));

            using (BeginDelayedSequence(loopStartTime - breakStartTime))
                overlay.PlayWarningAnimation(flashCount);
        }
    }
}
