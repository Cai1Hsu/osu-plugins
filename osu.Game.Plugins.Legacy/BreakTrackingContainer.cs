using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Utils;

namespace osu.Game.Plugins.Legacy;

public abstract partial class BreakTrackingContainer : Container
{
    public abstract void OnBreakStart();

    public abstract void OnBreakEnd();

    public virtual void OnGameSeeked()
    {
        if (IsBreakTime.Value)
            OnBreakStart();
        else
            OnBreakEnd();
    }

    [Resolved]
    private GameplayClockContainer? gameplayClockContainer { get; set; }

    public readonly Bindable<bool> IsBreakTime = new Bindable<bool>();
    public readonly IBindable<Period?> CurrentBreakPeriod = new Bindable<Period?>();

    private BreakTracker breakTracker = null!;

    protected virtual bool UseBreakTrackerClock => true;

    [BackgroundDependencyLoader]
    private void load(Player? player, BreakTracker? cachedBreakTracker)
    {
        breakTracker = player?.BreakOverlay.BreakTracker ?? cachedBreakTracker
            ?? throw new InvalidOperationException("BreakTrackingContainer requires a BreakTracker to function.");

        ((IBindable<bool>)IsBreakTime).BindTo(breakTracker.IsBreakTime);
        CurrentBreakPeriod.BindTo(breakTracker.CurrentPeriod);

        if (UseBreakTrackerClock)
        {
            Clock = breakTracker.Clock;
            ProcessCustomClock = false;
        }

        if (gameplayClockContainer is not null)
            gameplayClockContainer.OnSeek += OnGameSeeked;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        IsBreakTime.BindValueChanged(onBreakTimeChanged, true);
    }

    private void onBreakTimeChanged(ValueChangedEvent<bool> @event)
    {
        if (@event.NewValue)
        {
            // ensure first hit object appeared.
            if (breakTracker.CurrentPeriod.Value is not null)
                OnBreakStart();
        }
        else
        {
            OnBreakEnd();
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (gameplayClockContainer is not null)
            gameplayClockContainer.OnSeek -= OnGameSeeked;
    }
}
