using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Gameplay;

namespace osu.Plugin.LegacyExperience.Tests.Gameplay;

public partial class TestScenePlayfieldMask : OsuTestScene
{
    private PlayfieldMask mask = null!;

    [Cached]
    private BreakTracker breakTracker = new BreakTracker(0, new ScoreProcessor(new OsuRuleset()));

    [Resolved]
    private OsuConfigManager settings { get; set; } = null!;

    private OsuTextFlowContainer infoText = null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("add playfield mask", () =>
        {
            Clear();

            Add(mask = new PlayfieldMask
            {
                RelativeSizeAxes = Axes.Both,
            });

            Add(infoText = new OsuTextFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });

            void updateInfoText()
            {
                infoText.Clear();
                infoText.AddParagraph($"Top/Bottom Borders: {(mask.DisplayTopBottomBorders.Value ? "Visible" : "Hidden")}");
                infoText.AddParagraph($"Border Type: {mask.VerticalBorderType.Value}");
            }

            mask.DisplayTopBottomBorders.BindValueChanged(_ => updateInfoText());
            mask.VerticalBorderType.BindValueChanged(_ => updateInfoText(), true);
        });

        AddStep("Fade out", () => mask.FadeOut());
        AddStep("Fade in", () => mask.FadeIn());

        AddToggleStep("toggle top/bottom borders", v => mask.DisplayTopBottomBorders.Value = v);

        AddStep("Set border type: BlackBar", () => mask.VerticalBorderType.Value = PlayfieldMask.SideBorderType.BlackBar);
        AddStep("Set border type: Legacy", () => mask.VerticalBorderType.Value = PlayfieldMask.SideBorderType.LegacyMaskingBorder);

        var dimLevel = settings.GetBindable<double>(OsuSetting.DimLevel);

        AddSliderStep("Background dimming", 0.0, 1.0, 0.0, v => dimLevel.Value = v);
        AddToggleStep("Apply background dimming", v => mask.ApplyBackgroundDimming.Value = v);
    }
}