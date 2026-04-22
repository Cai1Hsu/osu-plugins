using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;
using SDL;

namespace osu.Plugin.Patches;

/// <summary>
/// This plugin enables the Windows Raw Keyboard hint in SDL3, which can reduce input latency for keyboards on Windows.
/// see https://wiki.libsdl.org/SDL3/SDL_HINT_WINDOWS_RAW_KEYBOARD and https://github.com/ppy/osu-framework/pull/6507.
/// There's known issue, refer to ppy/osu-framework/pull/6507.
/// </summary>
public partial class LowLatencyKeyboardPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        if (!OperatingSystem.IsWindows())
        {
            Logger.Log("Low latency keyboard patch is only supported on Windows, skipping.");
            return;
        }

        if (!FrameworkEnvironment.UseSDL3)
        {
            Logger.Log("Low latency keyboard hint is only supported on SDL3, considering set OSU_SDL3=1 to enable it.", LoggingTarget.Runtime, LogLevel.Important);
            return;
        }

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var host = game.Dependencies.Get<GameHost>();

            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD, "1"u8, SDL_HintPriority.SDL_HINT_OVERRIDE);

            int sdlVersion = SDL3.SDL_GetVersion();

            // see https://wiki.libsdl.org/SDL3/SDL_HINT_WINDOWS_RAW_KEYBOARD_EXCLUDE_HOTKEYS
            if (sdlVersion >= SDL3.SDL_VERSIONNUM(3, 4, 0))
            {
                // This makes Win-key blocker work when raw input is enabled.
                // after all low latency isn't meaningful for hotkeys
                SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD_EXCLUDE_HOTKEYS, "1"u8, SDL_HintPriority.SDL_HINT_OVERRIDE);
            }
            else
            {
                Logger.Log("SDL version does not support excluding hotkeys from raw keyboard input, Win-key blocker may not work properly when raw input is enabled.", LoggingTarget.Runtime, LogLevel.Important);
            }

            var hintValue = SDL3.SDL_GetHint(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD);
            Logger.Log($"Low latency keyboard hint value: {hintValue}", LoggingTarget.Runtime, LogLevel.Verbose);

            if (hintValue != "1")
                Logger.Log($"Failed to set low latency keyboard hint, current value {hintValue}, error: {SDL3.SDL_GetError()}", LoggingTarget.Runtime, LogLevel.Error);
        });
    }
}
