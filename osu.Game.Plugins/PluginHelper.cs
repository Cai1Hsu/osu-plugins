using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Screens;

namespace osu.Game.Plugins;

public static class PluginHelper
{
    /// <summary>
    /// Injects a dependency into the composite drawable if it does not already exist.
    /// Ensure to call this method on the update thread.
    /// </summary>
    /// <typeparam name="T">The type of the dependency to inject.</typeparam>
    /// <param name="composite">The composite drawable to inject into.</param>
    /// <param name="instance">The injected or existing instance.</param>
    /// <param name="factory">A factory function to create the instance if it does not exist.</param>
    /// <returns>True if a new instance was injected; false if an existing instance was found.</returns>
    public static bool InjectDependencies<T>(this CompositeDrawable composite, out T instance, Func<T> factory)
        where T : Drawable
    {
        if (composite.Dependencies.Get<T>() is T existing)
        {
            instance = existing;
            return false;
        }

        var dependencies = composite.Dependencies as DependencyContainer;

        Debug.Assert(dependencies != null);

        instance = factory();

        if (composite is Container container)
            container.Add(instance);
        else
            composite.AddInternal(instance);

        dependencies.CacheAs(instance);
        return true;
    }

    public delegate void ScreenSwitchedDelegate(IScreen oldScreen, IScreen newScreen);

    static void PerformOnceInternal(ScreenStack screenStack, ScreenSwitchedDelegate action, Func<Type, bool> shouldInvoke)
    {
        var currentScreen = screenStack.CurrentScreen;

        if (currentScreen is not null && shouldInvoke(currentScreen.GetType()))
        {
            switch (currentScreen)
            {
                case Drawable d when !d.IsLoaded:
                    d.OnLoadComplete += _ => action(currentScreen, currentScreen);
                    break;

                // TODO: Can we invoke immediately?
                case { } when ThreadSafety.IsUpdateThread:
                    action(currentScreen, currentScreen);
                    break;

                default:
                    var scheduler = currentScreen is Drawable drawable
                        ? drawable.GetScheduler()
                        : screenStack.GetScheduler();
                    // SAFETY: currentScreen is non-null guarded by the outer if condition.
                    scheduler.Add(() => action(currentScreen!, currentScreen!));
                    break;
            }
        }
        else
        {
            screenStack.ScreenPushed += newScreenArrives;
            screenStack.ScreenExited += newScreenArrives;
        }

        void newScreenArrives(IScreen oldScreen, IScreen newScreen)
        {
            if (newScreen is null)
                return;

            if (!shouldInvoke(newScreen.GetType()))
                return;

            if (newScreen != screenStack.CurrentScreen)
                return;

            // Unregister immediately to ensure single invocation.
            // events are fired on the update thread, so sequential invocations are safe.
            screenStack.ScreenPushed -= newScreenArrives;
            screenStack.ScreenExited -= newScreenArrives;

            if (newScreen is Drawable drawable)
                drawable.InvokeWhenReady(_ => action(oldScreen, newScreen));
            else
                // invoke immediately, this event is invoked on the update thread
                // we assume the screen is ready to use at this point
                action(oldScreen, newScreen);
        }
    }

    /// <summary>
    /// Performs an action once when the current screen or the next pushed/exited screen is of a specified type.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="action">The action to perform. The parameters are the old screen and the new screen.</param>
    /// <param name="screenTypes">The types of screens to listen for. If null or empty, all screen types are considered valid. Types are compared by exact type match.</param>
    public static void PerformOnceFromScreen(this OsuGame game, ScreenSwitchedDelegate action, IEnumerable<Type>? screenTypes = null)
    {
        screenTypes ??= Type.EmptyTypes;

        bool shouldInvoke(Type type)
        {
            bool hasAny = false;

            foreach (var t in screenTypes)
            {
                hasAny = true;

                if (t == type)
                    return true;
            }

            // If no types were specified, consider all types as valid.
            return !hasAny;
        }

        PerformOnceInternal(game.GetScreenStack(), action, shouldInvoke);
    }

    /// <summary>
    /// Performs an action once when the current screen is not of a specified type, or when the next pushed/exited screen is not of a specified type.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="action">The action to perform. The parameters are the old screen and the new screen.</param>
    /// <param name="banTypes">The types of screens to exclude. If null or empty, no screen types are excluded. Types are compared by assignability.</param>
    public static void PerformOnceExcludeScreen(this OsuGame game, ScreenSwitchedDelegate action, IEnumerable<Type>? banTypes = null)
    {
        banTypes ??= Type.EmptyTypes;

        bool shouldInvoke(Type type)
        {
            foreach (var t in banTypes)
            {
                if (t.IsAssignableFrom(type))
                    return false;
            }

            return true;
        }

        PerformOnceInternal(game.GetScreenStack(), action, shouldInvoke);
    }

    // This method exists to prevent compiled binaries from breaking when the original method signature changes.
    public static void InvokeWhenReady(this Drawable drawable, Action<Drawable> action, bool requiresUpdateThread = true)
       => drawable.InvokeWhenReady(action, null, requiresUpdateThread);

    public static void InvokeWhenReady(this Drawable drawable, Action<Drawable> action, Func<Drawable, Scheduler>? schedulerGetter, bool requiresUpdateThread = true)
    {
        switch (drawable.IsLoaded)
        {
            case true when !requiresUpdateThread || ThreadSafety.IsUpdateThread:
                action(drawable);
                break;

            case true:
                void scheduleAction() => action(drawable);

                // if the subtree the drawable is in is inactive,
                // this could result in action queuing.
                // We assume the caller is aware of this.
                var scheduler = schedulerGetter is null
                    ? drawable.GetScheduler()
                    : schedulerGetter(drawable);
                scheduler.Add(scheduleAction);
                break;

            default:
                drawable.OnLoadComplete += action;
                break;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ScreenStack")]
    public static extern ref OsuScreenStack GetScreenStack(this OsuGame game);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddInternal")]
    public static extern void AddInternal(this CompositeDrawable composite, Drawable drawable);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Scheduler")]
    public static extern Scheduler GetScheduler(this Drawable drawable);

    private static readonly FieldInfo[] logger_static_delegates = typeof(Logger)
        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType))
        .ToArray();

    /// <summary>
    /// Finds all game instances in the current process via static analysis.
    /// This allows you to inject into games even if you don't have direct access to the instance.
    /// This method could be quite time-consuming, use with caution.
    /// </summary>
    /// <returns>All possible game instances found. In most cases, you can assume only one instance exists.</returns>
    public static IEnumerable<Framework.Game> GetGameStatically()
        // Logger is probably the closest and easiest way for us to find active game instances.
        => logger_static_delegates
            .Select(f => f.GetValue(null) as Delegate)
            .Where(d => d is not null)
            .SelectMany(d => d!.GetInvocationList())
            .Select(d => d.Target)
            // Note that although usually only one game instance exists, testing environments may have multiple.
            // We leave the choice to the caller to decide how to handle multiple instances.
            .OfType<Framework.Game>();

    public static Texture? GetAutoSized(this TextureStore store, string lookup)
    {
        return store.Get($"{lookup}@2x") ?? store.Get(lookup);
    }

    public static Texture? GetAutoSized(this TextureStore store, string lookup, WrapMode wrapModeS, WrapMode wrapModeT)
    {
        return store.Get($"{lookup}@2x", wrapModeS, wrapModeT)
            ?? store.Get(lookup, wrapModeS, wrapModeT);
    }
}
