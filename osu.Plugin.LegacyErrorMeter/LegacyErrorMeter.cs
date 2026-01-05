using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play.HUD.HitErrorMeters;

namespace osu.Plugin.LegacyErrorMeter;

public partial class LegacyErrorMeter : HitErrorMeter
{
    public LegacyErrorMeterDrawable MeterDrawable { get; private set; } = null!;

    [SettingSource("Hide before first hit", "Whether to hide the hit error meter until the first hit object is judged.")]
    public Bindable<bool> HideBeforeFirstHit { get; } = new BindableBool(true);

    [BackgroundDependencyLoader]
    private void load(DrawableRuleset? ruleset)
    {
        AutoSizeAxes = Axes.Both;

        InternalChild = MeterDrawable = new LegacyErrorMeterDrawable();

        if (HitWindows is not null)
            MeterDrawable.SetHitWindows(HitWindows);

        if (ruleset is not null)
            Clock = ruleset.Clock;

        if (HideBeforeFirstHit.Value)
            this.FadeOut();
    }

    protected override void OnNewJudgement(JudgementResult judgement)
    {
        if (!judgement.IsHit || 
            !judgement.Type.IsScorable() || 
            judgement.Type.IsBonus() || 
            judgement.HitObject.HitWindows?.WindowFor(HitResult.Miss) == 0)
            return;

        this.FadeIn();
        MeterDrawable.ProcessJudgement(judgement.Type, judgement.TimeOffset);
    }

    public override void Clear() => MeterDrawable.ClearJudgements();
}
