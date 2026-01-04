using osu.Framework.Testing;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.PluginsLoader.Tests;

public partial class TestSceneOsuGame : OsuTestScene
{
    protected OsuGame Game = null!;

    [SetUpSteps]
    public virtual void SetUpSteps()
    {
        AddStep("Create new game instance", () =>
        {
            AddGame(Game = CreateNewGame());
        });
    }

    protected OsuGame CreateNewGame() => new TestOsuGame();
}
