using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Plugin.LegacyFpsDisplay.Tests;

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
