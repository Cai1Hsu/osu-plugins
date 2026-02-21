using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Audio;
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
                    // Seasonal events change is of a low frequency.
                    // In stable and lazer it only changes when you download a update, a restart is required obviously,
                    // so we usually read config eagerly at the loading time and never expect it to change during the lifetime of the scene. 
                    // To make change take effect immediately, we need to recreate the scene when the config changes.
                    // AudioEngine is a singleton so we have to create a local to reflect seasonal changes immediately.
                    var localAudioEngine = new AudioEngine();

                    c.Child = new DependencyProvidingContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        CachedDependencies =
                        [
                            (typeof(AudioEngine), localAudioEngine),
                        ],
                        Children = new Drawable[]
                        {
                            localAudioEngine,
                            logo = new OsuLogo
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            }
                        }
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
