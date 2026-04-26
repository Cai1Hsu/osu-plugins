using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Plugins;

namespace osu.Plugin.Patches;

/// <summary>
/// This plugin adds an "Unlimited FPS" option allowing players to remove the FPS cap and achieve higher frame rates if their hardware allows it. 
/// </summary>
public partial class UnlimitedFpsPlugin : OsuPlugin
{
    [SettingSource("Unlimited FPS", "Removes the FPS cap. May cause increased power consumption and heat generation.")]
    public Bindable<bool> RemoveFpsCap => Enabled;

    private Bindable<FrameSync> frameSync = null!;

    public override IEnumerable<Drawable>? CreateSettingsControls() => new Drawable[]
    {
        new Container
        {
            AutoSizeAxes = Axes.Y,
            RelativeSizeAxes = Axes.X,
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Unlimited FPS",
                    HintText = "Removes the FPS cap. May cause increased power consumption and heat generation.",
                    Current = RemoveFpsCap
                })
                {
                    Keywords = new[] { "unlimited", "fps", "frame", "rate", "cap", "vsync" },
                }
            }
        }
    };

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var settingsOverlay = game.Dependencies.Get<SettingsOverlay>();
            var frameworkConfig = game.Dependencies.Get<FrameworkConfigManager>();
            var host = game.Dependencies.Get<GameHost>();

            frameSync = frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync);

            RemoveFpsCap.BindValueChanged(_ => updateFrameSync());

            frameSync.BindValueChanged(_ => updateFrameSync(), true);

            void updateFrameSync()
            {
                bool unlimited = frameSync.Value is FrameSync.Unlimited && RemoveFpsCap.Value;

                if (host.AllowBenchmarkUnlimitedFrames == unlimited)
                    return;

                host.AllowBenchmarkUnlimitedFrames = unlimited;

                // related projects (ez2lazer) considers 8000 fps to be "effectively unlimited",
                // also the maximum poll rate of popular gaming devices is 8000 hz, 
                // so this should be a good enough upper bound for unlimited fps.
                const double max_fps = 8000;

                double target_fps = unlimited ? max_fps : 1000;

                // when disabling unlimited fps, the host reset limiter for update and draw threads automatically.
                if (unlimited || frameSync.Value is FrameSync.Unlimited)
                {
                    host.MaximumUpdateHz = target_fps;
                    host.MaximumDrawHz = target_fps;
                }

                // framework expects input and audio threads to always run at 1000 hz.
                host.AudioThread.ActiveHz = target_fps;

                // Must be done at last, the host schedules a task to reset the limiter on the InputThread (known as MainThread) when mutating MaximumUpdateHz
                // By setting it after, we ensure our task runs after the host's reset task, so that the limiter is correctly set to the target fps.
                host.InputThread.ActiveHz = target_fps;
            }
        });
    }
}
