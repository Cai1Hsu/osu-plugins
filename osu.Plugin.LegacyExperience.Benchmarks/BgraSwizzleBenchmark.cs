using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using osu.Plugin.LegacyExperience.Graphics;

namespace osu.Plugin.LegacyExperience.Benchmarks;

[MemoryDiagnoser]
public class BgraSwizzleBenchmark
{
    private byte[]? data;
    private GCHandle handle;
    private IntPtr pData;
    private int length;

    [Params(1024, 65536, 400 * 1024, 1024 * 1024)] // 1KB, 64KB, 0.4MB, 1MB
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        data = new byte[Size];
        new Random(42).NextBytes(data);
        handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        pData = handle.AddrOfPinnedObject();
        length = Size;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (handle.IsAllocated)
            handle.Free();
    }

    [Benchmark(Baseline = true)]
    public void Scalar32()
    {
        BitmapHelper.SwizzleToRgba32Scalar(pData, length);
    }

    [Benchmark]
    public void Scalar64()
    {
        BitmapHelper.SwizzleToRgba32Scalar_64(pData, length);
    }

    [Benchmark]
    public void Scalar_osu_stable()
    {
        BitmapHelper.SwizzleToRgba32Scalar_osu_stable(pData, length);
    }

    [Benchmark]
    public void Vector128()
    {
        if (System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated)
            BitmapHelper.SwizzleToRgba32Vector128(pData, length);
        else
            throw new NotSupportedException("Vector128 is not supported on this hardware.");
    }

    [Benchmark]
    public void Vector256()
    {
        if (System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated)
            BitmapHelper.SwizzleToRgba32Vector256(pData, length);
        else
            throw new NotSupportedException("Vector256 is not supported on this hardware.");
    }
}

