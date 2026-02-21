using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Plugin.LegacyExperience.Screens.Menu;
using osu.Plugin.LegacyExperience.Tests.Seasonal;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneOsuLogo : TestSceneWithBeatmap
{
    [Cached(typeof(IAmplitudesProvider))]
    private IAmplitudesProvider amplitudes = new AmplitudesProvider();

    private OsuLogo logo = null!;

    private SeasonalContainer seasonalContainer = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRange(new Drawable[]
        {
            (Drawable)amplitudes,
            seasonalContainer = new SeasonalContainer
            {
                RecreateScene = c =>
                {
                    c.Child = logo = new OsuLogo
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    };
                },
                RelativeSizeAxes = Axes.Both,
            },
        });
    }

    [Test]
    public void TestVisualisation()
    {
        AddStep("hide visualisation", () => logo.Visualisation.Hide());
        AddStep("show visualisation", () => logo.Visualisation.Show());
    }

    [Test]
    public void TestSeasonal() => seasonalContainer.TestSeasonal();
}
