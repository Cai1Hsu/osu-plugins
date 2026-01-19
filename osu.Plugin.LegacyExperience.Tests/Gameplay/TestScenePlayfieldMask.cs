using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
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
        });

        AddStep("Fade in", () => mask.FadeIn());
        AddStep("Fade out", () => mask.FadeOut());
    }
}