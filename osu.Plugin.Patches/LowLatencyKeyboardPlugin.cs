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

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var host = game.Dependencies.Get<GameHost>();

            if (!IsSDL3Window(host.Window))
            {
                Logger.Log("Low latency keyboard hint is only supported on SDL3, considering set OSU_SDL3=1 to enable it.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD, "1"u8, SDL_HintPriority.SDL_HINT_OVERRIDE);

            // This makes Win-key blocker work when raw input is enabled.
            // after all low latency isn't meaningful for hotkeys
            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD_EXCLUDE_HOTKEYS, "1"u8, SDL_HintPriority.SDL_HINT_OVERRIDE);

            var hintValue = SDL3.SDL_GetHint(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD);
            Logger.Log($"Low latency keyboard hint value: {hintValue}", LoggingTarget.Runtime, LogLevel.Verbose);

            if (hintValue != "1")
                Logger.Log($"Failed to set low latency keyboard hint, current value {hintValue}, error: {SDL3.SDL_GetError()}", LoggingTarget.Runtime, LogLevel.Error);
        });
    }

    private static bool IsSDL3Window(IWindow window)
    {
        var windowType = window.GetType();

        if (windowType.IsAssignableTo(SDL3Window_Type))
            return true;

        return windowType.FullName?.Contains("SDL3") ?? false;
    }

    private static readonly Type SDL3Window_Type = typeof(GameHost).Assembly.GetType("osu.Framework.Platform.SDL3.SDL3Window")!;
}
