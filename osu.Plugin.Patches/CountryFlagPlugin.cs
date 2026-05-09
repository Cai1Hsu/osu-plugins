using System.Collections;
using System.Diagnostics;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;
using osu.Game.Users;
using osu.Game.Users.Drawables;

namespace osu.Plugin.Patches;

/// <summary>
/// A plugin that replaces the Taiwanese flag with the Chinese flag to make someone happy, helpful when streaming in China.
/// This plugin is more about demostrating how to observer drawable creation and modify them, a test/framework is planned to be added in the future to make this process easier and more robust.
/// </summary>
public partial class CountryFlagPlugin : OsuPlugin
{
    public override string Description => "Replaces the Taiwanese flag with the Chinese flag to make someone happy.";

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame)
            return;

        gameBase.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            Stopwatch sw = Stopwatch.StartNew();

            // activator_cache is a ConcurrentDictionary, so that there maybe a tiny race window where multiple activators are created, and a latter one overrides our cached one.
            // Wait for all async loading completes so that the activator is cached and won't be created again.
            var loadLocks = game.ChildrenOfType<CompositeDrawable>()
                                .Where(static d => d.LoadState >= LoadState.Ready)
                                .Select(CompositeDrawableAccessor.Get_loadingComponents)
                                .Where(static c => c != null)
                                .SelectMany(static c => c)
                                .Where(static d => d.LoadState < LoadState.Ready)
                                .Select(DrawableAccessor.GetLoadLock)
                                .ToArray();

            Stopwatch lockAcquireSw = Stopwatch.StartNew();

            try
            {
                // acquire all locks to ensure no components are loading, and thus no activator is being created.
                foreach (var l in loadLocks)
                    Monitor.Enter(l);

                lockAcquireSw.Stop();

                ProxyDrawableFlagTypeActivator((obj, dependencies) =>
                {
                    var instance = (DrawableFlag)obj;
                    var textures = dependencies.Get<TextureStore>();

                    if (DrawableFlagAccessor.GetCountryCode(instance) is not CountryCode.TW)
                        return;

                    var enabled = Enabled.GetBoundCopy();

                    instance.add_OnDispose(enabled.UnbindAll);
                    enabled.BindValueChanged(e => hookupTexture(instance, textures, e.NewValue), true);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to hook DrawableFlag activator, flags will not be replaced.");
                return;
            }
            finally
            {
                lockAcquireSw.Stop();
                sw.Stop();

                foreach (var l in loadLocks)
                    Monitor.Exit(l);
            }

            Logger.Log($"CountryFlagPlugin loaded in {sw.Elapsed.TotalMilliseconds:0.000}ms (lock acquire time: {lockAcquireSw.Elapsed.TotalMilliseconds:0.000}ms)", level: LogLevel.Verbose);
        });
    }

    private static void hookupTexture(DrawableFlag drawableFlag, TextureStore textures, bool enableReplacement)
    {
        var countryCode = enableReplacement ? 
            CountryCode.CN : DrawableFlagAccessor.GetCountryCode(drawableFlag);

        // Copied from DrawableFlag.load

        string textureName = countryCode == CountryCode.Unknown ? "__" : countryCode.ToString();
        drawableFlag.Texture = textures.Get($@"Flags/{textureName}") ?? textures.Get(@"Flags/__");
    }

    private abstract partial class DrawableFlagAccessor : DrawableFlag
    {
        protected DrawableFlagAccessor(CountryCode countryCode) : base(countryCode)
        {
        }

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "countryCode")]
        internal static extern ref CountryCode GetCountryCode(DrawableFlag instance);
    }

    internal static void ProxyDrawableFlagTypeActivator(InjectDependencyDelegate postInjectDelegate)
    {
        if (postInjectDelegate is null)
            return;

        Debug.Assert(ThreadSafety.IsUpdateThread);

        if (DependencyActivatorAccessor.activator_cache[typeof(DrawableFlag)] is not { } cachedActivator)
        {
            using var instance = new DrawableFlag(CountryCode.Unknown);

            cachedActivator = null!;

            if (instance is ISourceGeneratedDependencyActivator)
            {
                DependencyActivatorAccessor.initialiseSourceGeneratedActivators(null!, instance);
                cachedActivator = DependencyActivatorAccessor.activator_cache[typeof(DrawableFlag)];

                // the target type may not be source generated but base type may be source generated,
                // which still resulted null cachedActivator, create a reflection activator as fallback.
            }

            cachedActivator ??= DependencyActivatorAccessor.getActivator(null!, typeof(DrawableFlag));
        }

        var injectionActivators = DependencyActivatorAccessor.get_injectionActivators(cachedActivator);

        // in tests scene, we may proxy multiple times, ensure only one activator exists in the list to avoid multiple hooking.
        injectionActivators.RemoveAll(d => d.Method == postInjectDelegate.Method);
        injectionActivators.Add(postInjectDelegate);
    }

    private abstract partial class DrawableAccessor : Drawable
    {
        [PrivateAccessor(PrivateAccessorKind.Field, Name = "LoadLock")]
        internal static extern object GetLoadLock(Drawable instance);
    }

    private abstract partial class CompositeDrawableAccessor : CompositeDrawable
    {
        protected CompositeDrawableAccessor()
        {
        }

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "loadingComponents")]
        internal static extern WeakList<Drawable> Get_loadingComponents(CompositeDrawable instance);
    }

    internal class DependencyActivatorAccessor
    {
        private const string DependencyActivatorTypeFullName = "osu.Framework.Allocation.DependencyActivator";

        internal static readonly IDictionary activator_cache;

        static DependencyActivatorAccessor()
        {
            activator_cache = get_activator_cache(null!);
        }

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "injectionActivators")]
        internal static extern ref List<InjectDependencyDelegate> get_injectionActivators([PrivateAccessorType(DependencyActivatorTypeFullName)] object activator);

        [PrivateAccessor(PrivateAccessorKind.StaticField, Name = nameof(activator_cache))]
        internal static extern IDictionary get_activator_cache([PrivateAccessorType(DependencyActivatorTypeFullName)] object typeRef);

        [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "initialiseSourceGeneratedActivators")]
        internal static extern void initialiseSourceGeneratedActivators([PrivateAccessorType(DependencyActivatorTypeFullName)] object typeRef, object candidate);

        [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "getActivator")]
        internal static extern object getActivator([PrivateAccessorType(DependencyActivatorTypeFullName)] object typeRef, Type targetType);
    }
}
