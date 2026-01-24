using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;

namespace osu.Game.Plugins.Skins;

public static class SkinEditorExtensions
{
    /// <summary>
    /// Register a skin component type from your plugin to be available in the skin editor for a specific <see cref="GlobalSkinnableContainerLookup"/>.
    /// </summary>
    /// <param name="skinEditorOverlay">The skin editor overlay.</param>
    /// <param name="componentTypes">The skin component types to register.</param>
    /// <param name="lookup">The lookup specifying where the component should be registered.</param>
    /// <exception cref="ArgumentException">Thrown when any of <paramref name="componentTypes"/> is not a valid <see cref="ISerialisableDrawable"/>.</exception>
    public static void RegisterSkinComponents(this SkinEditorOverlay skinEditorOverlay, Type[] componentTypes, GlobalSkinnableContainerLookup lookup)
    {
        static bool isInvalid(Type t) => !typeof(ISerialisableDrawable).IsAssignableFrom(t) ||
            !typeof(Drawable).IsAssignableFrom(t) ||
            t.IsAbstract ||
            t.IsInterface ||
            // Requires generic types to be closed (e.g., GenericClass<>)
            t.IsGenericTypeDefinition ||
            t.GetConstructor(Type.EmptyTypes) is null;

        if (componentTypes.Where(isInvalid).ToArray() is Type[] invalidTypes && invalidTypes.Length > 0)
        {
            throw new ArgumentException($"The following types are not valid skin component: {string.Join(", ", invalidTypes.Select(t => t.Name))}", nameof(componentTypes));
        }

        // editor instance sometimes gets recreated, but sometimes not
        // so we keep a reference to the last seen instance to avoid redundant registrations
        // use weak reference to avoid memory leak
        WeakReference<SkinEditor>? lastSkinEditor = null;

        // simple event registration is safe to execute outside of update thread, same as below
        skinEditorOverlay.InvokeWhenReady(registerOverlayActiveEvent, false);

        void registerOverlayActiveEvent(Drawable d)
        {
            var skinEditorOverlay = (SkinEditorOverlay)d;
            var overlayScheduler = skinEditorOverlay.Scheduler;

            skinEditorOverlay.State.BindValueChanged(v =>
            {
                if (v.NewValue is not Visibility.Visible)
                    return;

                // SAFETY: we expect we expect the overlay's `PopIn` is triggered by the value changed event,
                // and we expect our code to be executed after the overlay's.
                overlayScheduler.Add(handleNewSkinEditor);
            });
        }

        // SkinEditor is recreated new each time the overlay is shown, so we need to re-register our event each time.
        void handleNewSkinEditor()
        {
            var skinEditor = skinEditorOverlay.getInternalSkinEditor();

            if (skinEditor is null)
                return;

            // We've already registered for this SkinEditor instance, skip.
            if (lastSkinEditor is not null &&
                lastSkinEditor.TryGetTarget(out var existing) &&
                ReferenceEquals(existing, skinEditor))
                return;

            lastSkinEditor = new WeakReference<SkinEditor>(skinEditor);

            // Wait until SkinEditor is loaded so that our event will be invoked after skinEditor's.
            // We don't want modifications to `selectedTarget` during skin editor's loading phase.
            skinEditor.InvokeWhenReady(registerSkinTargetEvent, false);

        }

        void registerTypeToSidebar(Container<EditorSidebarSection> componentsSidebar)
        {
            foreach (var child in componentsSidebar.Children)
            {
                if (child is not SkinComponentToolbox section)
                    continue;

                RulesetInfo? ruleset = section.getInternalRuleset();

                if (ruleset is null != lookup.Ruleset is null)
                    continue;

                if (ruleset is not null && !ruleset.Equals(lookup.Ruleset))
                    continue;

                foreach (var componentType in componentTypes)
                {
                    // Exceptions are caught inside `attemptAddComponent` so our try seems redundant.
                    // Keep it anyway in case the internal implementation changes in the future.
                    try
                    {
                        section.attemptAddComponentToolbox(componentType);
                    }
                    catch
                    {
                        // ignore, as the developer/user can see their types are not added in the toolbox.
                    }
                }
            }
        }

        void registerSkinTargetEvent(Drawable d)
        {
            var skinEditor = (SkinEditor)d;
            var componentsSidebar = skinEditor.getInternalComponentsSidebar();

            if (componentsSidebar is null)
                return;

            var selectedTarget = skinEditor.getInternalSelectedTarget();
            var skinEditorScheduler = skinEditor.Scheduler;

            selectedTarget.BindValueChanged(v =>
            {
                // There's a manual trigger to load the selected target during skin editor loading.
                if (v.OldValue is null)
                    return;

                // Ruleset is not considered in target selection, so we only need to check Lookup equality.
                // Ruleset is later compared inside `registerTypeToSidebar`.
                // same as below
                if (v.NewValue is not null && v.NewValue.Lookup == lookup.Lookup)
                {
                    // SAFETY: This hack relies on our code being excuted after the skinEditor's
                    // and we expect toolboxes are recreated the next frame after target change.
                    // Also, schedule allows our code to be executed on the update thread,
                    // which is required for modifying the sidebar.
                    skinEditorScheduler.Add(() =>
                    {
                        registerTypeToSidebar(componentsSidebar);
                    });
                }
            }, true);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "attemptAddComponent")]
    static extern void attemptAddComponentToolbox(this SkinComponentToolbox toolbox, Type componentType);

    // unsafe accessor is not applicable here as the concrete type of the sidebar is internal.
    // In iOS or Android's AOT compilation, dynamic method generation is not supported, so we resort to reflection here.
    // Reflection is not a big performance concern as this is only once for every SkinEditor instance in every registration.
    static readonly FieldInfo componentsSidebarFieldInfo = typeof(SkinEditor).GetField("componentsSidebar", BindingFlags.NonPublic | BindingFlags.Instance)!;
    static Container<EditorSidebarSection>? getInternalComponentsSidebar(this SkinEditor skinEditor)
    {
        Debug.Assert(componentsSidebarFieldInfo is not null);

        // I kinda hate the Container<T> not being covariant...
        // at least non-generic version container should be assignable from generic one.
        // But sadly, non of them are.
        return componentsSidebarFieldInfo.GetValue(skinEditor) as Container<EditorSidebarSection>;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ruleset")]
    static extern ref RulesetInfo? getInternalRuleset(this SkinComponentToolbox lookup);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "selectedTarget")]
    static extern ref Bindable<GlobalSkinnableContainerLookup?> getInternalSelectedTarget(this SkinEditor skinEditor);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "skinEditor")]
    static extern ref SkinEditor? getInternalSkinEditor(this SkinEditorOverlay overlay);
}
