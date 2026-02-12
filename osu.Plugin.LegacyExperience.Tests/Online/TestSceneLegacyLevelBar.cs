using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Online;

namespace osu.Plugin.LegacyExperience.Tests.Online;

public partial class TestSceneLegacyLevelBar : OsuTestScene
{
    private LevelProgressBar levelBar = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddStep("create level bar", () =>
        {
            levelBar = new LevelProgressBar
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Progress = { Value = 0.5 }
            };

            Child = levelBar;
        });

        AddSliderStep("set level", 0, 1, 0.5, v => levelBar?.Progress.Value = v);
    }
}
