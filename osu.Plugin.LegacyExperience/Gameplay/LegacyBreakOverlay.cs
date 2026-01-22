using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets;
using osu.Framework.Graphics;
using osu.Game.Utils;

namespace osu.Plugin.LegacyExperience.Gameplay;

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
    private GameplayClockContainer? gameplayClock { get; set; } = null!;

    private readonly BindableDouble healthValue = new BindableDouble(1)
    {
        MinValue = 0,
        MaxValue = 1,
        Default = 1,
    };

    private double globalPreemptTime = 0;

    private LegacyBreakOverlayDrawable overlay = null!;

    public LegacyBreakOverlay()
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
    }

    // passing/failing is generated when break begin
    // we should track rewinding ourselves
    public override bool RemoveCompletedTransforms => true;

    private double? firstHitObjectStartTime;

    [BackgroundDependencyLoader]
    private void load(IBindable<WorkingBeatmap> workingBeatmap, IBindable<IReadOnlyList<Mod>> mods, IBindable<RulesetInfo> rulesetInfo, GameplayState? gameplayState)
    {
        AddInternal(overlay = new LegacyBreakOverlayDrawable());

        var beatmap = workingBeatmap.Value.Beatmap;

        globalPreemptTime = calculateGlobalPreemptTime(beatmap.BeatmapInfo, mods.Value, rulesetInfo.Value);

        var firstHitObject = beatmap.HitObjects.OrderBy(h => h.StartTime).FirstOrDefault();

        if (firstHitObject is not null)
            firstHitObjectStartTime = firstHitObject.StartTime;

        scheduleCountDownAnimation();

        if (gameplayClock is not null)
            gameplayClock.OnSeek += onGameSeek;

        if (gameplayState is not null)
            healthValue.BindTo(gameplayState.HealthProcessor.Health);
    }

    private void onGameSeek()
    {
        Scheduler.Add(replayAnimations);
    }

    private void replayAnimations()
    {
        overlay.ClearAnimations();

        if (firstHitObjectStartTime.HasValue && Clock.CurrentTime <= firstHitObjectStartTime.Value)
            scheduleCountDownAnimation();

        var currentBreak = CurrentBreak.Value;

        if (currentBreak.HasValue)
            scheduleAnimationForBreak(currentBreak.Value);
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

    private void scheduleCountDownAnimation()
    {
        if (!firstHitObjectStartTime.HasValue)
            return;

        if (firstHitObjectStartTime.Value < 6000)
            return;

        // use integer to match stable's behavior
        int startTime = (int)firstHitObjectStartTime.Value - (int)globalPreemptTime - 900;
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
        // pass state is considered when HP bar ended above half in the last playtime section
        // see: https://osu.ppy.sh/wiki/en/Storyboard/Scripting/General_Rules#game-state
        // lazer's storyboard also uses health value to determine section pass/fail state
        // see: https://github.com/ppy/osu/blob/e2dd4d86b4a79232aa0c1c8e8c520dc4be7ec94d/osu.Game/Storyboards/Drawables/DrawableStoryboard.cs#L110-L111
        // however, according to wiki, Geki and Katu also affect section pass/fail state, but lazer doesn't consider them.
        // reference:
        // - Geki: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/Geki#osu%21
        // - Katu: https://osu.ppy.sh/wiki/en/Gameplay/Judgement/Katu#osu%21
        // Since lazer's scoreprocessor doesn't judge Geki/Katu for osu! ruleset, we simply ignore them here.
        return healthValue.Value >= 0.5;
    }

    private const double min_break_duration_for_section_ranking = 2880;

    public override void OnBreakStart()
    {
        var currentPeriod = CurrentBreak.Value;

        if (!currentPeriod.HasValue)
            return;

        scheduleAnimationForBreak(currentPeriod.Value);
    }

    private void scheduleAnimationForBreak(Period period)
    {
        // BreakTracker subtracts BreakOverlay.BREAK_FADE_DURATION from the end time to trigger the end of break earlier.
        // Using original value is confirmed to match osu!stable's behavior.
        double breakDuration = period.Duration + BreakOverlay.BREAK_FADE_DURATION;
        double breakStartTime = period.Start;
        double breakEndTime = period.End + BreakOverlay.BREAK_FADE_DURATION;

        playSectionRanking();
        playResumeWarningArrows();

        // Sometimes transparency get modified by other components, so we update it again here.
        Schedule(updateLazerBreakOverlayTransparency);

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
                overlay.PlayBreakRankingAnimation(IsSectionPassing());
        }

        void playResumeWarningArrows()
        {
            // stable uses integer for these timings, we keep consistent.
            int preemptCount = Math.Min(2, (int)(globalPreemptTime / 200));
            int flashCount = Math.Min(5, (int)((breakDuration + 200) / 200)) + preemptCount;
            int loopStartTime = (int)(breakEndTime - 200 * (flashCount - preemptCount));

            using (BeginAbsoluteSequence(loopStartTime))
                overlay.PlayWarningAnimation(flashCount);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (gameplayClock is not null)
            gameplayClock.OnSeek -= onGameSeek;
    }
}
