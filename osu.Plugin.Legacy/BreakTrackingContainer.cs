using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Lists;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Screens.Play;
using osu.Game.Utils;

namespace osu.Plugin.Legacy;

public abstract partial class BreakTrackingContainer : CompositeDrawable
{
    public virtual void OnBreakStart()
    {
    }

    public virtual void OnBreakEnd()
    {
    }

    public readonly IBindable<Period?> CurrentBreak = new Bindable<Period?>();

    private BreakTracker breakTracker = null!;

    // Setting this to false to allow animations rewinding on seeks.
    public override bool RemoveCompletedTransforms => false;

    protected virtual bool UseBreakTrackerClock => true;

    [BackgroundDependencyLoader]
    private void load(Player? player, BreakTracker? cachedBreakTracker, IBindable<WorkingBeatmap> workingBeatmap)
    {
        breakTracker = player?.BreakOverlay.BreakTracker ?? cachedBreakTracker
            ?? throw new InvalidOperationException("BreakTrackingContainer requires a BreakTracker to function.");

        CurrentBreak.BindTo(breakTracker.CurrentPeriod);

        if (UseBreakTrackerClock)
        {
            Clock = breakTracker.Clock;
            ProcessCustomClock = false;
        }

        var beatmap = workingBeatmap.Value.Beatmap;

        ScheduleBreakAnimations(beatmap.Breaks);
    }

    protected virtual void ScheduleBreakAnimations(IReadOnlyList<BreakPeriod> breaks)
    {
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        CurrentBreak.BindValueChanged(onBreakTimeChanged);
    }

    private void onBreakTimeChanged(ValueChangedEvent<Period?> @event)
    {
        if (@event.NewValue.HasValue)
            OnBreakStart();
        else
            OnBreakEnd();
    }
}
