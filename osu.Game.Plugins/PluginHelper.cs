using System.Diagnostics;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddInternal")]
    public static extern void AddInternal(this CompositeDrawable composite, Drawable drawable);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Scheduler")]
    public static extern Scheduler GetScheduler(this Drawable drawable);
}