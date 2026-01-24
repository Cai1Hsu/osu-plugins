using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Game.Screens;
using osu.Game.Screens.Footer;

namespace osu.Game.Plugins;

public static class OsuGameExtensions
{
    extension(OsuGame @this)
    {
        public ref OsuScreenStack ScreenStack
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref GetScreenStack(@this);
        }

        public ScreenFooter ScreenFooter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetScreenFooter(@this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Field, Name = "ScreenStack")]
    public static extern ref OsuScreenStack GetScreenStack(OsuGame game);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_ScreenFooter")]
    public static extern ScreenFooter GetScreenFooter(OsuGame game);
}
