using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.PluginsLoader;

public class PluginLoaderRuleset : Ruleset
{
    public override string ShortName => "Plugins";
    public override string Description => "Provide plugin functionality";

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new DummyBeatmapConverter(beatmap, this);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new DummyDifficultyCalculator(RulesetInfo, beatmap);

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => throw new NotImplementedException("This ruleset is not meant to be played.");

    public override IEnumerable<Mod> GetModsFor(ModType type) => Array.Empty<Mod>();

    public override Drawable CreateIcon() => new OsuHook()
    {
        RelativeSizeAxes = Axes.Both,
        Content = new SpriteIcon
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Icon = FontAwesome.Solid.PuzzlePiece
        }
    };

    private class DummyDifficultyCalculator : DifficultyCalculator
    {
        public DummyDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
            => new DifficultyAttributes(mods, 0);

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
            => Array.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
            => Array.Empty<Skill>();
    }

    private class DummyBeatmapConverter : BeatmapConverter<HitObject>
    {
        public DummyBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => true;
    }
}
