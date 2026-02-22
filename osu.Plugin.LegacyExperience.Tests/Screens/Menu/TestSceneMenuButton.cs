using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Screens.Menu;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneMenuButton : OsuTestScene
{
    private Container buttonContainer = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(buttonContainer = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });

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

        foreach (var button in buttons)
        {
            AddStep($"add {button} button", () =>
            {
                buttonContainer.Child = new MenuButton
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Name = button,
                };
            });
        }
    }
}
