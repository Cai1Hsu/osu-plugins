using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework.Platform;

namespace osu.Game.Plugins;

public static class OsuGameBaseExtensions
{
    extension(OsuGameBase @this)
    {
        public Storage Storage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetStorage(@this);
            // This property has a setter, but we don't expose it as it may break things.
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Storage")]
    public static extern Storage GetStorage(OsuGameBase gameBase);
}