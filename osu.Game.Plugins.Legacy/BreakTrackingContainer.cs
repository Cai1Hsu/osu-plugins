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
        if (IsBreakTime.Value && CurrentBreakPeriod.Value is not null)
            OnBreakStart();
        else
            OnBreakEnd();
    }

    [Resolved]
    private GameplayClockContainer? gameplayClockContainer { get; set; }

    public readonly Bindable<bool> IsBreakTime = new Bindable<bool>();
    public readonly IBindable<Period?> CurrentBreakPeriod = new Bindable<Period?>();

    private BreakTracker? breakTracker;

    protected virtual bool UseBreakTrackerClock => true;

    [BackgroundDependencyLoader]
    private void load(Player? player)
    {
        if (player is not null)
        {
            breakTracker = player.BreakOverlay.BreakTracker;

            ((IBindable<bool>)IsBreakTime).BindTo(player.IsBreakTime);
            CurrentBreakPeriod.BindTo(breakTracker.CurrentPeriod);

            if (UseBreakTrackerClock)
            {
                Clock = breakTracker.Clock;
                ProcessCustomClock = false;
            }
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
            if (breakTracker is null ||
                // ensure first hit object appeared.
                breakTracker.CurrentPeriod.Value is not null)
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
