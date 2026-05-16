using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.Plugins;

public readonly record struct SkinComponentContainerLookup(GlobalSkinnableContainers Layer, RulesetInfo? Ruleset = null);

public static class SkinEditorExtensions
{
    /// <summary>
    /// Registers custom skin components to be added to the skin editor toolbox. This allows plugins to extend the skin editor with custom components, which can be used to create more complex and interactive skins.
    /// </summary>
    /// <param name="componentTypes">The types of the components to be added. They must be public, non-abstract, non-interface classes that implement both <see cref="ISerialisableDrawable"/> and <see cref="Drawable"/>.</param>
    /// <param name="lookup">The lookup key for the container to which the components should be added.</param>
    public static void RegisterSkinComponents(Type[] componentTypes, SkinComponentContainerLookup lookup)
    {
        throwIfAnyInvalid(componentTypes);

        if (lookup.Layer is GlobalSkinnableContainers.SongSelect && lookup.Ruleset is not null)
            lookup = new SkinComponentContainerLookup(lookup.Layer, null);

        lock (registeredComponents)
        {
            if (!registeredComponents.TryGetValue(lookup, out var registered))
                registeredComponents[lookup] = registered = new List<Type>();

            // We are not responsible for duplicate registrations, since components are most likely defined in the same assembly, 
            // and the caller can easily deduplicate them before calling this method.
            registered.AddRange(componentTypes);
        }
    }

    private static void throwIfAnyInvalid(Type[] types)
    {
        var invalidTypes = types.Where(isInvalid).ToArray();

        if (invalidTypes.Length > 0)
            throw new ArgumentException($"The following types are not valid skin component: {string.Join(", ", invalidTypes.Select(t => t.Name))}", nameof(types));

        static bool isInvalid(Type t) => t.IsInterface || t.IsAbstract || !t.IsPublic ||
                                         !typeof(ISerialisableDrawable).IsAssignableFrom(t) ||
                                         !typeof(Drawable).IsAssignableFrom(t);
    }

    public static IReadOnlyDictionary<SkinComponentContainerLookup, List<Type>> RegisteredComponents => registeredComponents;

    private static readonly Dictionary<SkinComponentContainerLookup, List<Type>> registeredComponents = new();

    private static readonly IDependencyActivatorProxy skinComponentToolboxActivatorProxy = DependencyActivatorProxyFactory.GetProxy(typeof(SkinComponentToolbox));

    static SkinEditorExtensions()
    {
        skinComponentToolboxActivatorProxy.OnInjected += onSkinComponentToolboxLoad;
    }

    private static void onSkinComponentToolboxLoad(object target, IReadOnlyDependencyContainer dependencies)
    {
        SkinComponentToolbox toolbox = (SkinComponentToolbox)target;

        Type[] componentTypes;

        lock (registeredComponents)
        {
            var key = new SkinComponentContainerLookup(toolbox.ContainerLookup.Lookup, toolbox.Ruleset);

            if (!registeredComponents.TryGetValue(key, out var registered))
                return;

            componentTypes = registered.ToArray();
        }

        foreach (var component in componentTypes)
            toolbox.AttemptAddComponent(component);
    }

    [SuppressMessage("Usage", "CA2255", Justification = "Intentionally used to ensure early registration of the proxy.")]
    [ModuleInitializer]
    internal static void RegisterProxy()
    {
        _ = skinComponentToolboxActivatorProxy;
    }
}

[SuppressMessage("Style", "OFSG001", Justification = "Not a dependency injection candidate.")]
internal static class SkinComponentToolboxExtensions
{
    extension(SkinComponentToolbox @this)
    {
        public RulesetInfo? Ruleset => SkinComponentToolboxAccessor.getInternalRuleset(@this);
        public SkinnableContainer Target => SkinComponentToolboxAccessor.getInternalTarget(@this);
        public GlobalSkinnableContainerLookup ContainerLookup => @this.Target.Lookup;

        public void AttemptAddComponent(Type type) => SkinComponentToolboxAccessor.attemptAddComponent(@this, type);
    }

    private abstract class SkinComponentToolboxAccessor : SkinComponentToolbox
    {
        [SuppressMessage("Style", "IDE0051", Justification = "Intentionally unused.")]
        private SkinComponentToolboxAccessor(SkinnableContainer target, RulesetInfo? ruleset) : base(target, ruleset) { }

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "ruleset")]
        internal static extern RulesetInfo? getInternalRuleset(SkinComponentToolbox instance);

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "target")]
        internal static extern SkinnableContainer getInternalTarget(SkinComponentToolbox instance);

        [PrivateAccessor(PrivateAccessorKind.Method, Name = "attemptAddComponent")]
        internal static extern void attemptAddComponent(SkinComponentToolbox instance, Type type);
    }
}
