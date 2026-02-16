using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using NUnit.Framework;
using osu.Plugin.LegacyExperience.Graphics;

namespace osu.Plugin.LegacyExperience.Tests.Graphics;

public class SwizzleToRgba32Tests
{
    private byte[] source = null!;
    private byte[] expected = null!;

    // create a big enough source array to test all SIMD paths (up to 256 bits = 32 bytes)
    const int data_length = 512;

    [SetUp]
    public void Setup()
    {
        source = new byte[data_length];
        expected = new byte[data_length];

        for (int i = 0; i < source.Length; i += 4)
        {
            // just to make sure we have some variation in the data, but it doesn't matter for the test
            byte offset = (byte)(i / 4 % 250);

            source[i] = (byte)(0 + offset);
            source[i + 1] = (byte)(1 + offset);
            source[i + 2] = (byte)(2 + offset);
            source[i + 3] = (byte)(3 + offset);
        }

        for (int i = 0; i < expected.Length; i += 4)
        {
            byte offset = (byte)(i / 4 % 250);

            expected[i] = (byte)(2 + offset);
            expected[i + 1] = (byte)(1 + offset);
            expected[i + 2] = (byte)(0 + offset);
            expected[i + 3] = (byte)(3 + offset);
        }
    }

    private unsafe void swizzleTest(Action<IntPtr, int> swizzleMethod)
    {
        fixed (byte* pSource = source)
        {
            swizzleMethod((IntPtr)pSource, source.Length);
        }

        Assert.That(source, Is.EqualTo(expected));
    }

    [Test]
    public void TestsStableReference() => swizzleTest(BitmapHelper.SwizzleToRgba32Scalar_osu_stable);

    [Test]
    public void TestsScalar32() => swizzleTest(BitmapHelper.SwizzleToRgba32Scalar);

    [Test]
    public void TestsScalar64() => swizzleTest(BitmapHelper.SwizzleToRgba32Scalar_64);

    [Test]
    public void TestsVector128()
    {
        if (!Vector128.IsHardwareAccelerated)
            Assert.Ignore("Vector128 hardware acceleration not supported on this platform.");

        swizzleTest(BitmapHelper.SwizzleToRgba32Vector128);
    }

    [Test]
    public void TestsVector256()
    {
        if (!Vector256.IsHardwareAccelerated)
            Assert.Ignore("Vector256 hardware acceleration not supported on this platform.");

        swizzleTest(BitmapHelper.SwizzleToRgba32Vector256);
    }
}
