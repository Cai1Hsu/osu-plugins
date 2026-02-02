using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework.Platform;

namespace osu.Game.Plugins;

[SuppressMessage("Style", "OFSG001", Justification = "This class doesn't contain classes intended to be used as Drawables")]
public static class OsuGameBaseExtensions
{
    extension(OsuGameBase @this)
    {
        public Storage Storage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetStorage(@this);
        }
    }

    public static Storage GetStorage(OsuGameBase gameBase) => OsuGameBaseDerived.get_Storage(gameBase);

    // see OsuGameExtensions.OsuGameDerived for explanation.
    private class OsuGameBaseDerived : OsuGameBase
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Storage")]
        internal static extern Storage get_Storage(OsuGameBase gameBase);
    }
}
