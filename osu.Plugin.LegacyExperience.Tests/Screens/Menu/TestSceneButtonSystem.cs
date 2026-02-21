using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Plugin.LegacyExperience.Screens.Menu;
using osu.Game.Input;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using NUnit.Framework;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneButtonSystem : TestSceneWithBeatmap
{
    [Cached(typeof(IAmplitudesProvider))]
    private AmplitudesProvider amplitudesProvider = new AmplitudesProvider();

    [Cached]
    private IdleTracker idleTracker = new IdleTracker(1000);

    private Container container = null!;
    private ButtonSystem buttonSystem = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRange(new Drawable[]
        {
            amplitudesProvider,
            idleTracker,
            container = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            }
        });
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create button system", () =>
        {
            container.Child = buttonSystem = new ButtonSystem
            {
                RelativeSizeAxes = Axes.Both,
            };
        });
    }

    [Test]
    public void TestFadeButtonsExcept()
    {
        string[] buttons =
        [
            "play",
            "edit",
            "options",
            "exit",
            "freeplay",
            "multiplayer",
            "charts",
            "back",
        ];

        foreach (var name in buttons)
        {
            AddStep($"fade buttons except {name}", () => buttonSystem.FadeButtonsExcept(name));
        }
    }
}
