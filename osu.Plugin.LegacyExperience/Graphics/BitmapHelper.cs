using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Versioning;

namespace osu.Plugin.LegacyExperience.Graphics;

internal static class BitmapHelper
{
    [SupportedOSPlatform("windows")]
    public static void SwizzleToRgba32(BitmapData bitmapData)
    {
        int length = bitmapData.Stride * bitmapData.Height;

        // Runtime-aware dispatch based on .NET version
        // .NET 9+ CLR has significant SIMD optimizations, earlier versions should use scalar
        // Note that running a .NET 8 compiled binary on .NET 9 CLR will not benefit from SIMD optimizations in our tests.
#if NET9_0_OR_GREATER
        // We found that Vector128 is always the fastest on .NET 9+ CLR, even on AVX2-capable hardware.
        // Vector128 is usually 2.5 ~ 4x faster than SwizzleToRgba32Scalar, 6 ~ 7x faster than stable's version.
        // Vector256 is fast usually, but hardly ever faster than Vector128, and can be much slower on some machines, so we use Vector128 as the default for .NET 9+.
        if (Vector128.IsHardwareAccelerated)
            SwizzleToRgba32Vector128(bitmapData.Scan0, length);
        else if (Vector256.IsHardwareAccelerated)
            // i guess there's no machine that supports AVX2 but not SSE2, but just in case...
            SwizzleToRgba32Vector256(bitmapData.Scan0, length);
        else
#endif
        // .NET 8 and earlier: use Scalar32 as fallback (most reliable performance)
        // This version is still much faster(>2x on my system) than stable's implementation.
        // The 64-bit version can be a little faster(less than 5%) in some cases but it varies a lot even on the same machine, 
        // so we use the more consistent 32-bit version.
        SwizzleToRgba32Scalar(bitmapData.Scan0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal unsafe static void SwizzleToRgba32Vector256(IntPtr pData, int length)
    {
        // repeating pattern of 2, 1, 0, 3 to shuffle BGRA to RGBA, then repeating every 4 bytes (1 pixel) and every 16 pixels (256 bits)
        Vector256<byte> shuffleMask = Vector256.Create(
            (byte)2, 1, 0, 3, (byte)6, 5, 4, 7,
            (byte)10, 9, 8, 11, (byte)14, 13, 12, 15,
            (byte)18, 17, 16, 19, (byte)22, 21, 20, 23,
            (byte)26, 25, 24, 27, (byte)30, 29, 28, 31
        );

        int vectorSize = Vector256<byte>.Count;

        byte* ptr = (byte*)pData;
        byte* endPtr = ptr + length;
        byte* vectorEndPtr = ptr + (length - (length % vectorSize));

        while (ptr < vectorEndPtr)
        {
            Vector256<byte> bgra = Vector256.Load(ptr);
            Vector256<byte> rgba = Vector256.Shuffle(bgra, shuffleMask);

            rgba.Store(ptr);

            ptr += vectorSize;
        }

        // tail
        SwizzleToRgba32Scalar((IntPtr)ptr, (int)(endPtr - ptr));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal unsafe static void SwizzleToRgba32Vector128(IntPtr pData, int length)
    {
        Vector128<byte> shuffleMask = Vector128.Create(
            (byte)2, 1, 0, 3, (byte)6, 5, 4, 7,
            (byte)10, 9, 8, 11, (byte)14, 13, 12, 15
        );

        int vectorSize = Vector128<byte>.Count;
        byte* ptr = (byte*)pData;
        byte* endPtr = ptr + length;
        byte* vectorEndPtr = ptr + (length - (length % vectorSize));

        while (ptr < vectorEndPtr)
        {
            Vector128<byte> bgra = Vector128.Load(ptr);
            Vector128<byte> rgba = Vector128.Shuffle(bgra, shuffleMask);

            rgba.Store(ptr);

            ptr += vectorSize;
        }

        // tail
        SwizzleToRgba32Scalar((IntPtr)ptr, (int)(endPtr - ptr));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal unsafe static void SwizzleToRgba32Scalar(IntPtr pData, int length)
    {
        byte* ptr = (byte*)pData;
        byte* endPtr = ptr + length;

        while (ptr < endPtr)
        {
            uint bgra = Unsafe.Read<uint>(ptr);

            // The following code performs 2103 byte shuffling to convert BGRA to RGBA format.
            // Using BSWAP, then ROL8 to achieve the desired result.
            // Reference: https://gist.github.com/Logan007/39811f0cb3acd41adcd2d19e831c69f6

            // reverse endianness first so that movbe can be used to eliminate explicit BSWAP.
            uint argb = BinaryPrimitives.ReverseEndianness(bgra);
            uint rgba = BitOperations.RotateLeft(argb, 8);

            Unsafe.Write(ptr, rgba);
            ptr += 4;
        }
    }

    // This version generates more instructions per-pixel than 32 bits version(3 compared to 1),
    // but it eliminates 1 read and 1 write for every 2 pixels.
    // It can be very inperformant on 32-bit platforms due to the 64-bit operations so we only use it on 64-bit processes. 
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal unsafe static void SwizzleToRgba32Scalar_64(IntPtr pData, int length)
    {
        byte* ptr = (byte*)pData;
        byte* endPtr = ptr + length;

        while (ptr <= endPtr - 8)
        {
            // B0 G0 R0 A0 | B1 G1 R1 A1
            ulong bgra2 = Unsafe.Read<ulong>(ptr);

            // A1 R1 G1 B1 | A0 R0 G0 B0
            ulong x = BinaryPrimitives.ReverseEndianness(bgra2);

            // R0 G0 B0 A1 | R1 G1 B1 A0
            x = BitOperations.RotateRight(x, 24);

            // 00 00 00 A1 | 00 00 00 A0
            ulong alpha = x & 0x000000FF000000FF;

            // R0 G0 B0 00 | R1 G1 B1 00
            x ^= alpha;

            // A0 00 00 00 | A1 00 00 00
            alpha = BitOperations.RotateRight(alpha, 8);

            ulong rgba = x | alpha;

            Unsafe.Write(ptr, rgba);

            ptr += 8;
        }

        // handle remaining bytes (up to 4 bytes)
        SwizzleToRgba32Scalar((IntPtr)ptr, (int)(endPtr - ptr));
    }

    // the version used in osu!stable, kept for benchmarking and reference.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal unsafe static void SwizzleToRgba32Scalar_osu_stable(IntPtr pData, int length)
    {
        byte* ptr = (byte*)pData;
        byte* endPtr = ptr + length;

        for (; ptr < endPtr; ptr += 4)
        {
            *(int*)ptr = ptr[2] | (ptr[1] << 8) | (*ptr << 16) | (ptr[3] << 24);
            // flag &= ptr3[3] == 0;
        }
    }
}
