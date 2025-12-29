using System.Diagnostics;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

    /// <summary>
    /// Performs an action once when the current screen or the next pushed/exited screen is of a specified type.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="action">The action to perform. The parameters are the old screen and the new screen.</param>
    /// <param name="screenTypes">The types of screens to listen for. If null or empty, all screen types are considered valid. Types are compared by exact type match.</param>
    public static void PerformOnceFromScreen(this OsuGame game, Action<IScreen, IScreen> action, IEnumerable<Type>? screenTypes = null)
    {
        screenTypes ??= Array.Empty<Type>();

        bool IsValidType(Type type)
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

        var screenStack = game.GetScreenStack();
        var currentScreen = screenStack.CurrentScreen;

        if (IsValidType(currentScreen.GetType()))
        {
            // TODO: Can we invoke immediately?
            if (ThreadSafety.IsUpdateThread)
                action(currentScreen, currentScreen);
            else
                game.GetScheduler().Add(() => action(currentScreen, currentScreen));
            return;
        }

        void newScreenArrives(IScreen oldScreen, IScreen newScreen)
        {
            if (!IsValidType(newScreen.GetType()))
                return;

            screenStack.ScreenPushed -= newScreenArrives;
            screenStack.ScreenExited -= newScreenArrives;

            action(oldScreen, newScreen);
        }

        screenStack.ScreenPushed += newScreenArrives;
        screenStack.ScreenExited += newScreenArrives;
    }

    /// <summary>
    /// Performs an action once when the current screen is not of a specified type, or when the next pushed/exited screen is not of a specified type.
    /// </summary>
    /// <param name="game">The game instance.</param>
    /// <param name="action">The action to perform. The parameters are the old screen and the new screen.</param>
    /// <param name="banTypes">The types of screens to exclude. If null or empty, no screen types are excluded. Types are compared by assignability.</param>
    public static void PerformOnceExcludeScreen(this OsuGame game, Action<IScreen, IScreen> action, IEnumerable<Type>? banTypes = null)
    {
        banTypes ??= Array.Empty<Type>();

        bool IsExcludedType(Type type)
        {
            foreach (var t in banTypes)
            {
                if (t.IsAssignableFrom(type))
                    return true;
            }

            return false;
        }

        var screenStack = game.GetScreenStack();
        var currentScreen = screenStack.CurrentScreen;

        if (!IsExcludedType(currentScreen.GetType()))
        {
            // TODO: Can we invoke immediately?
            if (ThreadSafety.IsUpdateThread)
                action(currentScreen, currentScreen);
            else
                game.GetScheduler().Add(() => action(currentScreen, currentScreen));
            return;
        }

        void newScreenArrives(IScreen oldScreen, IScreen newScreen)
        {
            if (IsExcludedType(newScreen.GetType()))
                return;

            screenStack.ScreenPushed -= newScreenArrives;
            screenStack.ScreenExited -= newScreenArrives;

            action(oldScreen, newScreen);
        }

        screenStack.ScreenPushed += newScreenArrives;
        screenStack.ScreenExited += newScreenArrives;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ScreenStack")]
    public static extern ref OsuScreenStack GetScreenStack(this OsuGame game);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddInternal")]
    public static extern void AddInternal(this CompositeDrawable composite, Drawable drawable);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Scheduler")]
    public static extern Scheduler GetScheduler(this Drawable drawable);
}