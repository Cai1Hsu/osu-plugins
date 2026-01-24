using System.Diagnostics;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;

namespace osu.Game.Plugins;

public static class CompositeDrawableExtensions
{
    extension(CompositeDrawable @this)
    {
        public Scheduler SchedulerAfterChildren
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetSchedulerAfterChildren(@this);
        }

        public IReadOnlyList<Drawable> InternalChildren
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetInternalChildren(@this);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => SetInternalChildren(@this, value);
        }

        public Drawable InternalChild
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetInternalChild(@this);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => SetInternalChild(@this, value);
        }

        /// <summary>
        /// Injects a dependency into the composite drawable if it does not already exist.
        /// Ensure to call this method on the update thread.
        /// </summary>
        /// <typeparam name="T">The type of the dependency to inject.</typeparam>
        /// <param name="instance">The injected or existing instance.</param>
        /// <param name="factory">A factory function to create the instance if it does not exist.</param>
        /// <returns>True if a new instance was injected; false if an existing instance was found.</returns>
        public bool InjectDependency<T>(out T instance, Func<T> factory)
            where T : Drawable
        {
            @this.EnsureChildMutationAllowed();

            if (@this.Dependencies.Get<T?>() is T existing)
            {
                instance = existing;
                return false;
            }

            var dependencies = @this.Dependencies as DependencyContainer;

            Debug.Assert(dependencies != null);

            instance = factory();

            if (@this is Container container)
                container.Add(instance);
            else
                @this.AddInternal(instance);

            dependencies.CacheAs(instance);
            return true;
        }

        public void AddInternal(Drawable drawable)
        {
            if (PluginHelper.IsIACTSupported)
                AddInternal_Accessor(@this, drawable);
            else
                // AddRangeInternal can be broken by IgnoresAccessChecksToAttribute.
                // hope that CLR stack-allocates the array to avoid memory allocations.
                @this.AddRangeInternal(new[] { drawable });

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [PrivateAccessor(PrivateAccessorKind.Method, Name = "AddInternal")]
            static extern void AddInternal_Accessor(CompositeDrawable composite, Drawable drawable);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "AddRangeInternal")]
    public static extern void AddRangeInternal(this CompositeDrawable composite, IEnumerable<Drawable> drawable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "ContainsInternal")]
    public static extern bool ContainsInternal(this CompositeDrawable composite, Drawable drawable);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "RemoveInternal")]
    public static extern bool RemoveInternal(this CompositeDrawable composite, Drawable drawable, bool disposeImmediately);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_InternalChildren")]
    public static extern IReadOnlyList<Drawable> GetInternalChildren(CompositeDrawable composite);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "set_InternalChildren")]
    public static extern void SetInternalChildren(CompositeDrawable composite, IReadOnlyList<Drawable> children);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_InternalChild")]
    public static extern Drawable GetInternalChild(CompositeDrawable composite);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "set_InternalChild")]
    public static extern void SetInternalChild(CompositeDrawable composite, Drawable child);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "ChangeInternalChildDepth")]
    public static extern void ChangeInternalChildDepth(this CompositeDrawable composite, Drawable child, float newDepth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_SchedulerAfterChildren")]
    public static extern Scheduler GetSchedulerAfterChildren(CompositeDrawable composite);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "EnsureChildMutationAllowed")]
    private static extern void EnsureChildMutationAllowed(this CompositeDrawable composite);
}
