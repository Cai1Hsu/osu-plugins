using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Plugins;

namespace osu.Game.Rulesets.PluginsLoader.Tests;

[TestFixture]
[NonParallelizable]
public partial class PluginHelperTests
{
    private static TemporaryNativeStorage CreateTestStorage() => new("PluginHelperTests");

    [Test]
    [NonParallelizable]
    public void TestGetGameStatically_Resolved()
    {
        using var storage = CreateTestStorage();
        using var game = new MockedOsuGame(storage); // events registered

        var foundGames = PluginHelper.GetGameStatically();

        // We are experiencing null object reference with NUnit,
        // so we use manual assertion, same as below.
        Assert.That(foundGames.Contains(game));
    }

    [Test]
    [NonParallelizable]
    public void TestGetGameStatically_MultipleGames()
    {
        using var storage = CreateTestStorage();

        // remember to dispose to unregister events
        using var game1 = new MockedOsuGame(storage);
        using var game2 = new MockedOsuGame(storage);

        var foundGames = PluginHelper.GetGameStatically();

        Assert.That(foundGames.Contains(game1));
        Assert.That(foundGames.Contains(game2));
    }

    [Test]
    [NonParallelizable]
    public void TestGetGameStatically_NoGame()
    {
        var foundGames = PluginHelper.GetGameStatically();

        Assert.That(foundGames.Count() == 0);
    }

    private partial class MockedOsuGame : OsuGame
    {
        public MockedOsuGame(Storage? storage = null)
        {
            // we have to ensure sentry logger setup or throws null reference exceptions when disposing
            SetupLogging(storage, storage);
        }
    }
}
