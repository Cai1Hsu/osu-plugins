using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform.SDL3;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;
using osu.Game.Screens.Play;
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
            CancelActivation("Low latency keyboard patch is only supported on Windows, skipping.", false);
            return;
        }

        if (!FrameworkEnvironment.UseSDL3)
        {
            CancelActivation("Low latency keyboard hint is only supported on SDL3, considering set OSU_SDL3=1 to enable it.", false);
            return;
        }

        // see https://wiki.libsdl.org/SDL3/SDL_HINT_WINDOWS_RAW_KEYBOARD_EXCLUDE_HOTKEYS
        bool supportWinKeyExclusion = SDL3.SDL_GetVersion() >= SDL3.SDL_VERSIONNUM(3, 4, 0);
        bool firstEnable = true;

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var localPlayInfo = game.Dependencies.Get<ILocalUserPlayInfo>();
            var localPlayingState = localPlayInfo.PlayingState.GetBoundCopy();

            Enabled.BindValueChanged(v => updateEnabledState(v.NewValue, localPlayingState.Value is LocalUserPlayingState.Playing));
            localPlayingState.BindValueChanged(v => updateEnabledState(Enabled.Value, v.NewValue is LocalUserPlayingState.Playing), true);
        });

        void updateEnabledState(bool pluginEnabled, bool userPlaying)
        {
            updateRawKeyboardState(pluginEnabled);
            updateWinKeyExclusionState(pluginEnabled && userPlaying);

            if (firstEnable && pluginEnabled)
            {
                if (!supportWinKeyExclusion)
                {
                    Logger.Log("SDL version does not support excluding hotkeys from raw keyboard input, Win-key blocker may not work properly when raw input is enabled.", LoggingTarget.Runtime, LogLevel.Important);
                    return;
                }

                firstEnable = false;
            }
        }

        void updateRawKeyboardState(bool enable)
        {
            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD, enable ? "1"u8 : "0"u8, SDL_HintPriority.SDL_HINT_OVERRIDE)
                .LogErrorIfFailed();
        }

        void updateWinKeyExclusionState(bool enable)
        {
            if (!supportWinKeyExclusion)
                return;

            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD_EXCLUDE_HOTKEYS, enable ? "1"u8 : "0"u8, SDL_HintPriority.SDL_HINT_OVERRIDE)
                .LogErrorIfFailed();
        }
    }
}
