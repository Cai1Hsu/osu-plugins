using System.Collections;
using System.Diagnostics;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics.Textures;
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

        gameBase.InvokeWhenReady(_ =>
        {
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

            // reflection path
            if (instance is not ISourceGeneratedDependencyActivator)
                cachedActivator = DependencyActivatorAccessor.getActivator(null!, typeof(DrawableFlag));
            else
            {
                DependencyActivatorAccessor.initialiseSourceGeneratedActivators(null!, instance);
                cachedActivator = DependencyActivatorAccessor.getActivator(null!, typeof(DrawableFlag));
            }
        }

        Debug.Assert(cachedActivator is not null);

        var injectionActivators = DependencyActivatorAccessor.get_injectionActivators(cachedActivator);

        injectionActivators.Add(postInjectDelegate);
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
