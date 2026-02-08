using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Overlays.Mods;
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
        AddStep("clear overlay", () =>
        {
            clearLazerOverlay();
            content.Clear();
        });
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

    private void clearLazerOverlay()
    {
        if (lazerSelect == null)
            return;

        content.Remove(lazerSelect, true);
        lazerSelect = null;
    }

    private UserModSelectOverlay? lazerSelect = null;

    // test consistency of mod selection overlay with lazer's mod selection overlay.
    [Test]
    public void TestAddLazerModSelectOverlay()
    {
        AddStep("show overlay", () =>
        {
            // make sure overlay appears at the top of the hierarchy to avoid being covered by the dialog.
            clearLazerOverlay();
            
            content.Add(lazerSelect = new UserModSelectOverlay()
            {
                RelativeSizeAxes = Axes.Both,
                State = { Value = Visibility.Visible },
                SelectedMods = { BindTarget = SelectedMods }
            });
        });
        AddStep("hide overlay", () => lazerSelect?.Hide());
    }
}
