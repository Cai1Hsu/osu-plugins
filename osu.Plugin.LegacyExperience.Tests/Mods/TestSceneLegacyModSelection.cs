using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Mods;

namespace osu.Plugin.LegacyExperience.Tests.Mods;

public partial class TestSceneLegacyModSelection : LocalSkinTestScene
{
    private SkinProvidingContainer content = null!;

    [Resolved]
    private Bindable<RulesetInfo> ruleset { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(content = new SkinProvidingContainer(new DefaultLegacySkin(this))
        {
            RelativeSizeAxes = Axes.Both,
        });

        AddStep("set ruleset to osu!", () => setRuleset(new OsuRuleset()));
        AddStep("set ruleset to taiko", () => setRuleset(new TaikoRuleset()));
        AddStep("set ruleset to catch", () => setRuleset(new CatchRuleset()));
        AddStep("set ruleset to mania", () => setRuleset(new ManiaRuleset()));
    }

    private void setRuleset(Ruleset ruleset) => this.ruleset.Value = ruleset.RulesetInfo;

    [SetUpSteps]
    public void SetupSteps()
    {
        AddStep("clear", () => content.Clear());
    }

    [Test]
    public void TestModsPositioning()
    {
        LegacyDialog? dialog = null;

        AddStep("add dialog", () =>
        {
            content.Clear();
            content.Add(dialog = new LegacyModSelection()
            {
                MultiplierText = { Text = "Score Multiplier: 1.00x" },
            });
            dialog.Show();
        });

        AddStep("hide dialog", () => dialog?.Hide());
    }
}
