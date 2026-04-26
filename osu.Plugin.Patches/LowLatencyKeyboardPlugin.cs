using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform.SDL3;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
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
    [SettingSource("Try to fix Win-key", "When enabled, the plugin will also try to exclude hotkeys (like Win-key) from raw keyboard input when the user is playing, which may help with issues like the Start menu opening when pressing the Win-key. Requires SDL 3.4 or higher.")]
    public Bindable<bool> FixWinKey { get; } = new BindableBool(true);

    private Bindable<bool> blockWinkey = null!;

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
        if (SDL3.SDL_GetVersion() < SDL3.SDL_VERSIONNUM(3, 4, 0))
        {
            FixWinKey.Value = false;
            FixWinKey.Disabled = true;
        }

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var config = game.Dependencies.Get<OsuConfigManager>();
            blockWinkey = config.GetBindable<bool>(OsuSetting.GameplayDisableWinKey);

            var localPlayInfo = game.Dependencies.Get<ILocalUserPlayInfo>();
            var localPlayingState = localPlayInfo.PlayingState.GetBoundCopy();

            void update() => updateEnabledState(localPlayingState.Value);

            blockWinkey.BindValueChanged(_ => update());
            Enabled.BindValueChanged(_ => update());
            FixWinKey.BindValueChanged(_ => update());
            localPlayingState.BindValueChanged(_ => update(), true);
        });

        void updateEnabledState(LocalUserPlayingState playingState)
        {
            bool userPlaying = playingState is LocalUserPlayingState.Playing;

            updateRawKeyboardState(Enabled.Value);
            updateWinKeyExclusionState(Enabled.Value && userPlaying && FixWinKey.Value && blockWinkey.Value);
        }

        void updateRawKeyboardState(bool enable)
        {
            Logger.Log($"Setting Windows Raw Keyboard to {(enable ? "enabled" : "disabled")}.", level: LogLevel.Debug);

            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD, enable ? "1"u8 : "0"u8, SDL_HintPriority.SDL_HINT_OVERRIDE)
                .LogErrorIfFailed();
        }

        void updateWinKeyExclusionState(bool enable)
        {
            Logger.Log($"Setting Win-key exclusion to {(enable ? "enabled" : "disabled")}.", level: LogLevel.Debug);

            SDL3.SDL_SetHintWithPriority(SDL3.SDL_HINT_WINDOWS_RAW_KEYBOARD_EXCLUDE_HOTKEYS, enable ? "1"u8 : "0"u8, SDL_HintPriority.SDL_HINT_OVERRIDE)
                .LogErrorIfFailed();
        }
    }
}
