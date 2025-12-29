using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Overlays;
using osu.Game.Screens.Play;

namespace osu.Plugin.StableIntegrationPlugin;

public partial class StableIntegrationManager : CompositeDrawable
{
    [Resolved]
    private GameHost host { get; set; } = null!;

    [Resolved]
    private ILocalUserPlayInfo localUserInfo { get; set; } = null!;

    private MusicStateObserver musicStateObserver = null!;
    private StableController? stableController = null!;

    public StableController? StableController => stableController;

    public IBindable<bool>? IsMusicPlaying => musicStateObserver?.IsPlaying;

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChildren = new Drawable[]
        {
            musicStateObserver = new MusicStateObserver(),
            (stableController = createStableController()) ?? Empty(),
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        host.Activated += () => requestedMuteStable = true;

        // TODO: an option?
        IsMusicPlaying?.BindValueChanged(v =>
        {
            if (v.NewValue)
                Scheduler.Add(ensureStableMuted);
        });

        localUserInfo.PlayingState.BindValueChanged(v =>
        {
            if (v.OldValue is LocalUserPlayingState.NotPlaying &&
                v.NewValue is LocalUserPlayingState.Playing or LocalUserPlayingState.Break)
                Scheduler.Add(ensureStableMuted);
        });

        if (RuntimeInfo.OS is not RuntimeInfo.Platform.Windows)
        {
            Logger.Log("Stable Integration is only supported on Windows.", level: LogLevel.Important);
        }
    }

    private bool requestedMuteStable = false;
    private Task? inProgressMuteTask = null;

    private void ensureStableMuted()
    {
        if (!requestedMuteStable)
            return;

        if (inProgressMuteTask is not null)
        {
            if (!inProgressMuteTask.IsCompleted)
                return;

            inProgressMuteTask = null;
        }

        requestedMuteStable = false;

        if (stableController is null)
            return;

        inProgressMuteTask = Task.Run(async () =>
        {
            try
            {
                await stableController.MuteStable()
                    .ConfigureAwait(false);
            }
            finally
            {
                inProgressMuteTask = null;
            }
        });
    }

    private static StableController? createStableController()
    {
        switch (RuntimeInfo.OS)
        {
            // It's rare to have stable installed on non-Windows platforms.
            // So we only provide implementation for Windows for now.
            case RuntimeInfo.Platform.Windows:
#pragma warning disable CA1416
                return new WindowsStableController();
#pragma warning restore CA1416

            default:
                return null;
        }
    }

    partial class MusicStateObserver : Drawable
    {
        [Resolved]
        private MusicController musicController { get; set; } = null!;

        private readonly BindableBool isPlaying = new BindableBool();

        public IBindable<bool> IsPlaying => isPlaying;

        [BackgroundDependencyLoader]
        private void load()
        {
            isPlaying.Value = musicController.IsPlaying;
        }

        protected override void Update()
        {
            base.Update();

            isPlaying.Value = musicController.IsPlaying;
        }
    }
}
