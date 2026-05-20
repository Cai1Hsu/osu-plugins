using AccessItEasy;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions.IEnumerableExtensions;
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
using osuTK;

namespace osu.Plugin.Patches;

/// <summary>
/// This plugin adds custom FPS limiters for each thread, allowing players to set different FPS limits for input, audio, update and draw threads when frame sync is set to unlimited.
/// It also has an option to sync all thread limiters with the input thread limiter for easier configuration.
/// </summary>
public partial class CustomLimiterPlugin : OsuPlugin
{
    [SettingSource("Sync All Threads")]
    public BindableBool SyncThreadLimits { get; } = new BindableBool(false);

    [SettingSource("Input Thread Limit")]
    public BindableDouble InputThreadLimit { get; } = createLimiterBindable();

    [SettingSource("Audio Thread Limit")]
    public BindableDouble AudioThreadLimit { get; } = createLimiterBindable();

    [SettingSource("Update Thread Limit")]
    public BindableDouble UpdateThreadLimit { get; } = createLimiterBindable();

    [SettingSource("Draw Thread Limit")]
    public BindableDouble DrawThreadLimit { get; } = createLimiterBindable();

    public override IEnumerable<Drawable>? CreateSettingsControls()
    {
        return new CustomLimiterSettings(this)
        {
            AutoSizeAxes = Axes.Y,
            RelativeSizeAxes = Axes.X,
            SyncThreadLimits = { BindTarget = SyncThreadLimits },
            InputThreadLimit = { BindTarget = InputThreadLimit },
            AudioThreadLimit = { BindTarget = AudioThreadLimit },
            UpdateThreadLimit = { BindTarget = UpdateThreadLimit },
            DrawThreadLimit = { BindTarget = DrawThreadLimit },
            Enabled = { BindTarget = Enabled },
        }.Yield();
    }

    private GameHost host = null!;
    private readonly Bindable<FrameSync> frameSync = new Bindable<FrameSync>();

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame)
            return;

        gameBase.InvokeWhenReady(d =>
        {
            var dependencies = ((CompositeDrawable)d).Dependencies;

            host = dependencies.Get<GameHost>();

            dependencies.Get<FrameworkConfigManager>().BindWith(FrameworkSetting.FrameSync, frameSync);

            // Ensure limiters are applied even if CustomLimiterSettings is not created.
            // CreateSettingsControls are only called the first time the settings overlay is opened, so if the user never opens it, the limiters will not be applied without this.
            ApplyLimiters();
        });
    }

    private void ApplyLimiters()
    {
        if (frameSync.Value is not FrameSync.Unlimited || !Enabled.Value)
        {
            GameHostAccessor.ApplyHostDefaultLimiter(host);
            return;
        }

        double applyValue(Bindable<double> original) => SyncThreadLimits.Value ? InputThreadLimit.Value : original.Value;

        host.MaximumUpdateHz = applyValue(UpdateThreadLimit);
        host.MaximumDrawHz = applyValue(DrawThreadLimit);
        host.AudioThread.ActiveHz = applyValue(AudioThreadLimit);

        // Must be done in the end, the host schedules a task to reset the limiter on the InputThread (known as MainThread) when mutating MaximumUpdateHz
        host.InputThread.ActiveHz = InputThreadLimit.Value;
    }

    private partial class CustomLimiterSettings : Container
    {
        public readonly Bindable<bool> Enabled = new Bindable<bool>();

        private readonly CustomLimiterPlugin plugin;

        private FillFlowContainer otherThreadsContainer = null!;

        public readonly BindableBool SyncThreadLimits = new BindableBool();
        public readonly BindableDouble InputThreadLimit = createLimiterBindable();
        public readonly BindableDouble AudioThreadLimit = createLimiterBindable();
        public readonly BindableDouble UpdateThreadLimit = createLimiterBindable();
        public readonly BindableDouble DrawThreadLimit = createLimiterBindable();

        private Bindable<FrameSync> frameSync = new Bindable<FrameSync>();
        private Bindable<ExecutionMode> executionMode = new Bindable<ExecutionMode>();

        private readonly Bindable<SettingsNote.Data?> note = new Bindable<SettingsNote.Data?>();

        private IEnumerable<BindableDouble> limiterBindables()
        {
            yield return UpdateThreadLimit;
            yield return DrawThreadLimit;
            yield return AudioThreadLimit;
            yield return InputThreadLimit;
        }

        public CustomLimiterSettings(CustomLimiterPlugin plugin)
        {
            this.plugin = plugin;
        }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            frameworkConfig.BindWith(FrameworkSetting.FrameSync, frameSync);
            frameworkConfig.BindWith(FrameworkSetting.ExecutionMode, executionMode);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                LayoutDuration = 200,
                LayoutEasing = Easing.OutQuint,
                Children = new Drawable[]
                {
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = "Sync All Threads",
                        HintText = "When enabled, changing the input thread limiter will apply the same value to all other thread limiters.",
                        Current = { BindTarget = SyncThreadLimits }
                    })
                    {
                        Keywords = new[] { "sync", "threads", "limit", "all" }
                    },
                    createLimiterSlider("Input", InputThreadLimit),
                    otherThreadsContainer = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Masking = true,
                        Children = new Drawable[]
                        {
                            createLimiterSlider("Audio", AudioThreadLimit),
                            createLimiterSlider("Update", UpdateThreadLimit),
                            createLimiterSlider("Draw", DrawThreadLimit),
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Child = new SettingsNote()
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = { BindTarget = note },
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (var bindable in limiterBindables())
                bindable.BindValueChanged(_ => plugin.ApplyLimiters());

            SyncThreadLimits.BindValueChanged(_ => updateAll());
            frameSync.BindValueChanged(_ => updateAll());
            executionMode.BindValueChanged(_ => updateAll());
            Enabled.BindValueChanged(_ => updateAll(), true);

            void updateAll()
            {
                updateUI();
                plugin.ApplyLimiters();
            }
        }

        private void updateUI()
        {
            bool singleThreaded = executionMode.Value is ExecutionMode.SingleThread;

            if (SyncThreadLimits.Value || singleThreaded)
            {
                otherThreadsContainer.AutoSizeAxes &= ~Axes.Y;
                otherThreadsContainer.Height = 0;
            }
            else
            {
                otherThreadsContainer.AutoSizeAxes |= Axes.Y;
            }

            bool applyCustomLimiter = frameSync.Value is FrameSync.Unlimited;

            foreach (var bindable in limiterBindables())
                bindable.Disabled = !applyCustomLimiter;

            if (!applyCustomLimiter)
            {
                note.Value = new SettingsNote.Data(
                    "Custom limiter is only active when Frame Sync is set to Unlimited.",
                    SettingsNote.Type.Warning);
            }
            else if (singleThreaded)
            {
                note.Value = new SettingsNote.Data(
                    "Other thread limiters are hidden in single-threaded mode.",
                    SettingsNote.Type.Warning);
            }
            else if (SyncThreadLimits.Value)
            {
                note.Value = new SettingsNote.Data(
                    "Other thread limiters will be synced with the Input Thread limiter.",
                    SettingsNote.Type.Informational);
            }
            else
            {
                note.Value = null;
            }
        }

        private static SettingsItemV2 createLimiterSlider(string threadName, BindableDouble bindable)
        {
            return new SettingsItemV2(new FormSliderBar<double>
            {
                Caption = $"{threadName} Thread",
                HintText = $"Set a custom FPS limit for the {threadName.ToLower()} thread. 0 is treated as unlimited.",
                KeyboardStep = 50,
                TooltipFormat = v => v == 0 ? "Unlimited" : $"{v:0} FPS",
                // Important to transfer the value on commit to avoid excessive updates when dragging the slider.
                // Also, there's a bug in FormSliderBar where it sync the slider value to current on load completes,
                // when current is disabled, this action throws and crashes the game.
                TransferValueOnCommit = true,
                Current = { BindTarget = bindable },
            })
            {
                Keywords = new[] { threadName.ToLower(), "thread", "fps", "limit", "custom" },
            };
        }
    }

    private abstract class GameHostAccessor : GameHost
    {
        protected GameHostAccessor(string gameName, HostOptions options) : base(gameName, options) { }

        public static void ApplyHostDefaultLimiter(GameHost host)
        {
            // call the original method to apply default limiter values according to the current frame sync mode.
            updateFrameSyncMode(host);

            // the method above does not reset the input thread limiter, so we need to reset it manually.
            host.AudioThread.ActiveHz = host_default_limiter;
            host.InputThread.ActiveHz = host_default_limiter;

            [PrivateAccessor(PrivateAccessorKind.Method, Name = "updateFrameSyncMode")]
            static extern void updateFrameSyncMode(GameHost host);
        }
    }

    internal const double host_default_limiter = 1000;

    private static BindableDouble createLimiterBindable()
    {
        return new BindableDouble
        {
            MinValue = 0, // 0 will be treated as unlimited in the limiter logic
            // related projects (ez2lazer) considers 8000 fps to be "effectively unlimited",
            // also the maximum poll rate of popular gaming devices is 8000 hz, 
            // so this should be a good enough upper bound for unlimited fps.
            MaxValue = 8000,
            Default = host_default_limiter,
            Value = host_default_limiter,
            Precision = 1,
        };
    }
}
