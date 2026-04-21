using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Graphics;
using osu.Game.Plugins;

namespace osu.Plugin.Patches;

/// <summary>
/// This plugin adds an "Unlimited FPS" option to the graphics settings, 
/// allowing players to remove the FPS cap and achieve higher frame rates if their hardware allows it. 
/// </summary>
public partial class UnlimitedFpsPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            // may extract this if more patches need configuration in the future, 
            // but for now we can just keep it here to avoid unnecessary complexity.
            game.CacheDependency(out var config, () => new PatchesConfigManager(game.Storage), false);

            var settingsOverlay = game.Dependencies.Get<SettingsOverlay>();
            var frameworkConfig = game.Dependencies.Get<FrameworkConfigManager>();
            var host = game.Dependencies.Get<GameHost>();

            var frameSync = frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
            var unlimitedFps = config.GetBindable<bool>(PatchesConfig.UnlimitedFps);

            settingsOverlay.InvokeWhenReady(d =>
            {
                var settingsOverlay = (SettingsOverlay)d;

                // Settings sections are lazily created after the first pop-in,
                // so we need to wait for the first update before we can add our setting.
                settingsOverlay.Add(new SectionsObserver(settingsOverlay.SectionsContainer, addSettings));
            });

            void addSettings()
            {
                var sections = settingsOverlay.SectionsContainer.Children;

                var graphicsSection = sections.OfType<GraphicsSection>().FirstOrDefault();
                var rendererSubSection = graphicsSection?.Children.OfType<RendererSettings>().FirstOrDefault();

                if (rendererSubSection is null)
                {
                    Logger.Log("Failed to find renderer settings section in graphics section.");
                    return;
                }

                rendererSubSection.InvokeWhenReady(d =>
                {
                    var rendererSubSection = (RendererSettings)d;

                    Container settingsContent;

                    // this simple approach adds the settings at the end of the renderer settings section, 
                    // which is good enough and avoids the complexity of trying to find the correct insertion point.
                    // also the config persists and it should be of low frequency to open the settings, UX is not a big concern here.
                    rendererSubSection.Add(new Container
                    {
                        AutoSizeAxes = Axes.Y,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeDuration = 200,
                        AutoSizeEasing = Easing.OutQuint,
                        Masking = true,
                        Child = settingsContent = new Container
                        {
                            AutoSizeAxes = Axes.Y,
                            RelativeSizeAxes = Axes.X,
                            Children = new Drawable[]
                            {
                                new SettingsItemV2(new FormCheckBox
                                {
                                    Caption = "Unlimited FPS",
                                    HintText = "Removes the FPS cap. May cause increased power consumption and heat generation.",
                                    Current = unlimitedFps
                                })
                                {
                                    Keywords = new[] { "unlimited", "fps", "frame", "rate", "cap", "vsync" },
                                },
                                // bindables are weakly bound and get collected if not kept alive by a reference.
                                new Box()
                                {
                                    frameSync = frameSync,
                                    unlimitedFps = unlimitedFps,
                                }
                            }
                        }
                    });

                    // layout immediately
                    frameSync.BindValueChanged(v =>
                    {
                        bool isUnlimited = v.NewValue is FrameSync.Unlimited;

                        if (isUnlimited)
                            settingsContent.BypassAutoSizeAxes &= ~Axes.Y;
                        else
                            settingsContent.BypassAutoSizeAxes |= Axes.Y;

                        unlimitedFps.Disabled = !isUnlimited;
                        updateFrameSync();
                    }, true);

                    unlimitedFps.BindValueChanged(_ => updateFrameSync(), true);
                });
            }

            void updateFrameSync()
            {
                bool unlimited = frameSync.Value is FrameSync.Unlimited
                    && unlimitedFps.Value
                    && !unlimitedFps.Disabled;

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

    private partial class SectionsObserver : Component
    {
        private readonly SectionsContainer<SettingsSection> sections;
        private Action? action;

        public SectionsObserver(SectionsContainer<SettingsSection> sections, Action? action)
        {
            this.sections = sections;
            this.action = action;
        }

        protected override void Update()
        {
            base.Update();

            if (sections.Children.Count is 0)
                return;

            // definitely we don't want to keep this drawable alive longer than necessary
            Expire();

            var a = action;
            action = null;

            try
            {
                a?.Invoke();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to inject settings drawable into {sections}.");
            }
        }
    }

    // used to keep a handle to a reference type object
    private partial class Box : Drawable
    {
        // use explicit type so that Drawable dispose unsubscribes the event for us.
        public Bindable<FrameSync> frameSync { get; set; } = default!;
        public Bindable<bool> unlimitedFps { get; set; } = default!;
    }
}