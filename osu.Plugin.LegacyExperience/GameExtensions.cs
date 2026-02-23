using osu.Game;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Audio;
using osu.Plugin.LegacyExperience.Graphics;
using osu.Plugin.LegacyExperience.Localisations;

namespace osu.Plugin.LegacyExperience;

public static class GameExtensions
{
    public static void EnsureLegacyDependencies(this OsuGameBase game)
    {
        game.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;

            // register TransitionManager at frist as the main menu requires it and it's quite lightweight compared to the other dependencies.
            game.InjectDependency(out TransitionManager _, static () => new());
            game.InjectDependency(out LegacyResourceManager _, static () => new());
            game.InjectDependency(out LegacyLocalisationManager _, static () => new());
            game.InjectDependency(out AudioEngine _, static () => new());
            game.CacheDependency(out INativeText _, CreateNativeText, true);
        });
    }

    private static NativeTextBase CreateNativeText()
    {
        if (OperatingSystem.IsWindows())
            return new GdipNativeText();

        return new ImageSharpNativeText();
    }
}
