using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using osu.Plugin.LegacyExperience.Graphics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;

namespace osu.Plugin.LegacyExperience.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
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
        BitmapHelper.SwizzleToRgba32Vector128(pData, length);
    }

    [Benchmark]
    public void Vector256()
    {
        BitmapHelper.SwizzleToRgba32Vector256(pData, length);
    }

    private class Config : ManualConfig
    {
        public Config()
        {
            AddFilter(new DisjunctionFilter(
                new NameFilter(name => name is nameof(Vector128)
                    && System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated),
                new NameFilter(name => name is nameof(Vector256)
                    && System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated),
                new NameFilter(name => name is not nameof(Vector128) and not nameof(Vector256))
            ));
        }
    }
}
