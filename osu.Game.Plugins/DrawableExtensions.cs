using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Threading;

namespace osu.Game.Plugins;

public static class DrawableExtensions
{
    extension(Drawable @this)
    {
        public Scheduler Scheduler
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetScheduler(@this);
        }

        public Action OnDispose
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetOnDispose(@this);
        }

        // This method exists to prevent compiled binaries from breaking when the original method signature changes.
        public void InvokeWhenReady(Action<Drawable> action, bool requiresUpdateThread = true)
           => @this.InvokeWhenReady(action, null, requiresUpdateThread);

        public void InvokeWhenReady(Action<Drawable> action, Func<Drawable, Scheduler>? schedulerGetter, bool requiresUpdateThread = true)
        {
            switch (@this.IsLoaded)
            {
                case true when !requiresUpdateThread || ThreadSafety.IsUpdateThread:
                    action(@this);
                    break;

                case true:
                    void scheduleAction() => action(@this);

                    // if the subtree the drawable is in is inactive,
                    // this could result in action queuing.
                    // We assume the caller is aware of this.
                    var scheduler = schedulerGetter is null
                        ? @this.Scheduler
                        : schedulerGetter(@this);
                    scheduler.Add(scheduleAction);
                    break;

                default:
                    @this.OnLoadComplete += action;
                    break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Scheduler")]
    public static extern Scheduler GetScheduler(Drawable drawable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Field, Name = "OnDispose")]
    public static extern ref Action GetOnDispose(Drawable drawable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "add_OnDispose")]
    public static extern void add_OnDispose(this Drawable drawable, Action value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "remove_OnDispose")]
    public static extern void remove_OnDispose(this Drawable drawable, Action value);
}
