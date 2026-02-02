using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Game.Screens;
using osu.Game.Screens.Footer;

namespace osu.Game.Plugins;

[SuppressMessage("Style", "OFSG001", Justification = "This class doesn't contain classes intended to be used as Drawables")]
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

    public static OsuScreenStack GetScreenStack(OsuGame game) => OsuGameDerived.get_ScreenStack(game);

    public static ScreenFooter GetScreenFooter(OsuGame game) => OsuGameDerived.get_ScreenFooter(game);

    // This class is used to bypass the limitation of IgnoreAccessChecksTo on some runtimes like Android(AOT).
    // On these runtimes, accessing private members of another type throws MemberAccessException.
    // However, for protected members, although exact type match is still required when writing C# code,
    // CLR actually performs a much looser check at runtime, allows access as long as the caller is derived from the target type.
    // This is because the FieldInfo is the same for all derived types.
    private class OsuGameDerived : OsuGame
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_ScreenFooter")]
        internal static extern ScreenFooter get_ScreenFooter(OsuGame game);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [PrivateAccessor(PrivateAccessorKind.Field, Name = "ScreenStack")]
        internal static extern OsuScreenStack get_ScreenStack(OsuGame game);
    }
}
