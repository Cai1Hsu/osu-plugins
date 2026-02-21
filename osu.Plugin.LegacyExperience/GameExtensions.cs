using osu.Game;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Audio;
using osu.Plugin.LegacyExperience.Graphics;
using osu.Plugin.LegacyExperience.Localisations;
using osu.Plugin.LegacyExperience.Seasonal;

namespace osu.Plugin.LegacyExperience;

public static class GameExtensions
{
    public static void EnsureLegacyDependencies(this OsuGameBase game)
    {
        game.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;

            game.CacheDependency(out ISeasonalConfig _, static () => new SeasonalUIConfig(), false);
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
