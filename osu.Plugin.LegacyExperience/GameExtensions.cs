using osu.Game;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Audio;
using osu.Plugin.LegacyExperience.Localisations;

namespace osu.Plugin.LegacyExperience;

public static class GameExtensions
{
    public static void EnsureLegacyDependencies(this OsuGameBase game)
    {
        game.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;

            game.InjectDependency(out LegacyResourceManager _, static () => new());
            game.InjectDependency(out LegacyLocalisationManager _, static () => new());
            game.InjectDependency(out AudioEngine _, static () => new());
        });
    }
}
