using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Plugin.LegacyExperience.Screens.Menu;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneOsuLogo : TestSceneWithBeatmap
{
    [Cached(typeof(IAmplitudesProvider))]
    private IAmplitudesProvider amplitudes = new AmplitudesProvider();

    private OsuLogo logo = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRange(new Drawable[]
        {
            (Drawable)amplitudes,
            logo = new OsuLogo
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }
        });
    }

    [Test]
    public void TestVisualisation()
    {
        AddStep("hide visualisation", () => logo.Visualisation.Hide());
        AddStep("show visualisation", () => logo.Visualisation.Show());
    }
}
