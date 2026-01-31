using System.Reflection;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Game.Screens;
using osu.Game.Screens.Footer;

namespace osu.Game.Plugins;

public static class OsuGameExtensions
{
    extension(OsuGame @this)
    {
        public OsuScreenStack ScreenStack
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetScreenStack(@this);
        }

        public ScreenFooter ScreenFooter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetScreenFooter(@this);
        }
    }

    private static readonly FieldInfo screenStackField = typeof(OsuGame)
        .GetField("ScreenStack", BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static OsuScreenStack GetScreenStack(OsuGame game)
    {
        if (PluginHelper.IsIACTSupported)
            return GetScreenStack_Accessor(game);
        else
            return (OsuScreenStack)screenStackField.GetValue(game)!;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [PrivateAccessor(PrivateAccessorKind.Field, Name = "ScreenStack")]
        static extern OsuScreenStack GetScreenStack_Accessor(OsuGame game);
    }

    private static readonly MethodInfo? screenFooterGetter = typeof(OsuGame)
        .GetProperty("ScreenFooter", BindingFlags.NonPublic | BindingFlags.Instance)?
        .GetMethod;

    public static ScreenFooter GetScreenFooter(OsuGame game)
    {
        if (PluginHelper.IsIACTSupported)
            return GetScreenFooter_Accessor(game);
        else
            return (ScreenFooter)screenFooterGetter!.Invoke(game, Array.Empty<object>())!;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_ScreenFooter")]
        static extern ScreenFooter GetScreenFooter_Accessor(OsuGame game);
    }
}
