using System.Reflection;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework;
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
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Storage GetStorage(OsuGameBase gameBase)
    {
        // `Storgae` can not broken by IgnoresAccessChecksToAttribute as it doesn't have internal modifier.
        if (PluginHelper.IsIACTSupported)
            return GetStorage_Accessor(gameBase);

        return (Storage)storageGetter?.Invoke(gameBase, Array.Empty<object>())!;

        [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_Storage")]
        static extern Storage GetStorage_Accessor(OsuGameBase gameBase);
    }

    private static readonly MethodInfo? storageGetter = typeof(OsuGameBase)
        .GetProperty("Storage", BindingFlags.NonPublic | BindingFlags.Instance)?
        .GetMethod;
}