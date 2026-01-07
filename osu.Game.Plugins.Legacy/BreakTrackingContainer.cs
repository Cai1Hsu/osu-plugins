using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Play;
using osu.Game.Utils;

namespace osu.Game.Plugins.Legacy;

public abstract partial class BreakTrackingContainer : Container
{
    public abstract void OnBreakStart();

    public abstract void OnBreakEnd();

    public virtual void OnGameSeeked()
    {
        onBreakTimeChanged(new ValueChangedEvent<Period?>(localCurrentBreak.Value, localCurrentBreak.Value));
    }

    protected void PlayAnimation(Action<Period> withBreak, Action withoutBreak)
    {
        var currentBreak = localCurrentBreak.Value;

        if (currentBreak.HasValue)
            withBreak(currentBreak.Value);
        else
            withoutBreak();
    }

    [Resolved]
    private GameplayClockContainer? gameplayClockContainer { get; set; }

    public readonly IBindable<Period?> CurrentBreak = new Bindable<Period?>();

    private readonly Bindable<Period?> localCurrentBreak = new Bindable<Period?>();

    protected IBindable<Period?> LocalCurrentBreak => localCurrentBreak;

    private BreakTracker breakTracker = null!;

    protected virtual bool UseBreakTrackerClock => true;

    [BackgroundDependencyLoader]
    private void load(Player? player, BreakTracker? cachedBreakTracker)
    {
        breakTracker = player?.BreakOverlay.BreakTracker ?? cachedBreakTracker
            ?? throw new InvalidOperationException("BreakTrackingContainer requires a BreakTracker to function.");

        CurrentBreak.BindTo(breakTracker.CurrentPeriod);

        if (UseBreakTrackerClock)
        {
            Clock = breakTracker.Clock;
            ProcessCustomClock = false;
        }

        if (gameplayClockContainer is not null)
            gameplayClockContainer.OnSeek += onGameSeeked;
    }

    // wait a frame to allow break tracker to update its state.
    private void onGameSeeked() => Scheduler.Add(OnGameSeeked);

    protected override void LoadComplete()
    {
        base.LoadComplete();

        CurrentBreak.BindValueChanged(onBreakTimeChanged);
    }

    private void onBreakTimeChanged(ValueChangedEvent<Period?> @event)
    {
        if (@event.NewValue.HasValue)
        {
            localCurrentBreak.Value = CurrentBreak.Value;

            // ensure first hit object appeared.
            if (breakTracker.CurrentPeriod.Value is not null)
                OnBreakStart();
        }
        else
        {
            OnBreakEnd();

            // Sync status later to allow access to period information in OnBreakEnd.
            localCurrentBreak.Value = CurrentBreak.Value;
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (gameplayClockContainer is not null)
            gameplayClockContainer.OnSeek -= onGameSeeked;
    }
}
