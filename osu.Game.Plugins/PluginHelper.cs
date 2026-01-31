using System.Reflection;
using osu.Framework;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Skinning;

namespace osu.Game.Plugins;

public static class PluginHelper
{
    // FIXME: this means full JIT support is required.
    // use better detection for AOT platforms if necessary.
    public static bool IsIACTSupported => RuntimeInfo.IsDesktop;

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
                        ? drawable.Scheduler
                        : screenStack.Scheduler;
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

        PerformOnceInternal(game.ScreenStack, action, shouldInvoke);
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

        PerformOnceInternal(game.ScreenStack, action, shouldInvoke);
    }

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

    /// <summary>
    /// Gets a texture from the store, prioritizing high-DPI versions if available.
    /// </summary>
    /// <param name="store">The texture store to retrieve the texture from.</param>
    /// <param name="lookup">The lookup string for the texture.</param>
    /// <returns>The retrieved texture, or null if not found.</returns>
    public static Texture? GetAutoSized(this TextureStore store, string lookup)
    {
        return store.Get($"{lookup}@2x") ?? store.Get(lookup);
    }

    /// <summary>
    /// Gets a texture from the store, prioritizing high-DPI versions if available.
    /// </summary>
    /// <param name="store">The texture store to retrieve the texture from.</param>
    /// <param name="lookup">The lookup string for the texture.</param>
    /// <param name="wrapModeS">The wrap mode for the S (U) texture coordinate.</param>
    /// <param name="wrapModeT">The wrap mode for the T (V) texture coordinate.</param>
    /// <returns>The retrieved texture, or null if not found.</returns>
    public static Texture? GetAutoSized(this TextureStore store, string lookup, WrapMode wrapModeS, WrapMode wrapModeT)
    {
        return store.Get($"{lookup}@2x", wrapModeS, wrapModeT)
            ?? store.Get(lookup, wrapModeS, wrapModeT);
    }

    /// <summary>
    /// Retrieves a texture from a skin, falling back to a texture store if not found.
    /// </summary>
    /// <remarks>This extension method is useful for retrieve legacy skin textures which Argon/Triangle don't provide.</remarks>
    /// <param name="skin">The skin to retrieve the texture from.</param>
    /// <param name="lookup">The lookup string for the texture.</param>
    /// <param name="textures">The texture store to fall back to if the skin does not provide the texture.</param>
    /// <param name="textureStoreLookupPrefix">An optional prefix to prepend to the lookup string when querying the texture store.</param>
    /// <returns>The retrieved texture, or null if not found.</returns>
    public static Texture? GetSkinTexture(this ISkin? skin, string lookup, TextureStore? textures = null, string? textureStoreLookupPrefix = null)
    {
        return skin?.GetTexture(lookup) ?? textures?.GetAutoSized(string.IsNullOrEmpty(textureStoreLookupPrefix) ? lookup : $"{textureStoreLookupPrefix}/{lookup}");
    }
}
