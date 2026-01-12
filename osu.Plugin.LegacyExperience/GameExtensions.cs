using osu.Game;
using osu.Game.Plugins;

namespace osu.Plugin.LegacyExperience;

public static class GameExtensions
{
    public static void EnsureLegacyResources(this OsuGameBase game)
    {
        game.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;
            game.InjectDependencies(out LegacyResourceManager _, () => new());
        });
    }
}
