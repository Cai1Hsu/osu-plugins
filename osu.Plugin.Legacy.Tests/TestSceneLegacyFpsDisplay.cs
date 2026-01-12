using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Plugin.Legacy.Tests;

public partial class TestSceneLegacyFpsDisplay : OsuTestScene
{
    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("Create fps display", () =>
        {
            Child = new LegacyFpsDisplay
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(200, 100),
            };
        });
    }
}
