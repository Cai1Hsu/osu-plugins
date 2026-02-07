using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring.Legacy;
using osu.Game.Rulesets.UI;

namespace osu.Plugin.LegacyExperience.Tests;

internal partial class TestRuleset : Ruleset, ILegacyRuleset
{
    public TestRuleset(RulesetInfo rulesetInfo)
        : base()
    {
        this.rulesetInfo = rulesetInfo;

        // The base constructor instantiates its own RulesetInfo, so we need to override it with our test one.
        // This is why we use null check for Description and Name even this.rulesetInfo is not nullable.
        ref_RulesetInfo_BackingField(this) = rulesetInfo;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<RulesetInfo>k__BackingField")]
    private static extern ref RulesetInfo ref_RulesetInfo_BackingField(Ruleset @this);

    private readonly RulesetInfo rulesetInfo;

    public override string Description => rulesetInfo?.Name ?? string.Empty;

    public override string ShortName => rulesetInfo?.Name ?? string.Empty;

    public int LegacyID => rulesetInfo?.OnlineID ?? -1;

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
    {
        throw new NotImplementedException();
    }

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
    {
        throw new NotImplementedException();
    }

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
    {
        throw new NotImplementedException();
    }

    public ILegacyScoreSimulator CreateLegacyScoreSimulator()
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<Mod> GetModsFor(ModType type)
    {
        throw new NotImplementedException();
    }
}
