using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;

namespace osu.Plugin.LegacyErrorMeter;

public partial class LegacyErrorMeter : HitErrorMeter
{
    private LegacyErrorMeterDrawable drawable = null!;

    public void UpdateHitWindows(HitWindows hitWindows)
    {
        drawable.SetHitWindows(hitWindows);
    }

    public LegacyErrorMeter()
    {
        AutoSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChild = drawable = new LegacyErrorMeterDrawable();

        if (HitWindows is not null)
            drawable.SetHitWindows(HitWindows);
    }

    protected override void OnNewJudgement(JudgementResult judgement)
    {
        if (!judgement.IsHit || !judgement.Type.IsScorable() || judgement.Type.IsBonus())
            return;

        drawable.ProcessJudgement(judgement.Type, judgement.TimeOffset);
    }

    public override void Clear()
    {
        drawable.ClearJudgements();
    }
}
