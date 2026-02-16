using osu.Game;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Graphics;
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
            game.CacheDependency(out INativeText _, CreateNativeText, true);
        });
    }

    private static NativeTextBase CreateNativeText()
    {
        return new ImageSharpNativeText();
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
