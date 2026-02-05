using osu.Game;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Localisations;

namespace osu.Plugin.LegacyExperience;

public static class GameExtensions
{
    public static void EnsureLegacyResources(this OsuGameBase game)
    {
        game.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;
            game.InjectDependency(out LegacyResourceManager _, () => new());
        });
    }

    public static void EnsureLegacyLocalisation(this OsuGameBase game)
    {
        game.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;
            game.InjectDependency(out LegacyLocalisationManager localisationManager, () => new());
        });
    }
}
