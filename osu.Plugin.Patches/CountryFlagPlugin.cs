using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework.Allocation;
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

    private OsuGame game = null!;

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        this.game = game;

        countryFlagActivatorProxy.OnInjected += onDrawableFlagLoad;
        game.add_OnDispose(() => countryFlagActivatorProxy.OnInjected -= onDrawableFlagLoad);
    }

    private void onDrawableFlagLoad(object instance, IReadOnlyDependencyContainer dependencies)
    {
        // not managed by us, skip.
        if (!ReferenceEquals(dependencies.Get<OsuGame>(), game))
            return;

        var drawableFlag = (DrawableFlag)instance;

        if (drawableFlag.CountryCode is not CountryCode.TW)
            return;

        var textures = dependencies.Get<TextureStore>();
        var enabled = Enabled.GetBoundCopy();

        drawableFlag.add_OnDispose(enabled.UnbindAll);
        enabled.BindValueChanged(e => hookupTexture(drawableFlag, textures, e.NewValue ? CountryCode.CN : null), true);
    }

    private static void hookupTexture(DrawableFlag drawableFlag, TextureStore textures, CountryCode? replacement)
    {
        var countryCode = replacement ?? drawableFlag.CountryCode;

        // Copied from DrawableFlag.load

        string textureName = countryCode == CountryCode.Unknown ? "__" : countryCode.ToString();
        drawableFlag.Texture = textures.Get($@"Flags/{textureName}") ?? textures.Get(@"Flags/__");
    }

    private static readonly IDependencyActivatorProxy countryFlagActivatorProxy = DependencyActivatorProxyFactory.GetProxy(typeof(DrawableFlag));

    [SuppressMessage("Usage", "CA2255", Justification = "Intentionally used to ensure early registration of the proxy.")]
    [ModuleInitializer]
    internal static void Initialize()
    {
        // ensure the proxy is created at this early stage
        _ = countryFlagActivatorProxy;
    }
}

[SuppressMessage("Style", "OFSG001", Justification = "Not a dependency injection candidate.")]
internal static class DrawableFlagExtensions
{
    extension(DrawableFlag @this)
    {
        public CountryCode CountryCode => DrawableFlagAccessor.GetCountryCode(@this);
    }

    private abstract class DrawableFlagAccessor : DrawableFlag
    {
        protected DrawableFlagAccessor(CountryCode countryCode) : base(countryCode) { }

        [PrivateAccessor(PrivateAccessorKind.Field, Name = "countryCode")]
        internal static extern ref CountryCode GetCountryCode(DrawableFlag instance);
    }
}
