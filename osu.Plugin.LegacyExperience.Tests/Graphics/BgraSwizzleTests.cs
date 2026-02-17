using System.Runtime.Intrinsics;
using NUnit.Framework;
using osu.Plugin.LegacyExperience.Graphics;

namespace osu.Plugin.LegacyExperience.Tests.Graphics;

public class BgraSwizzleTests
{
    /// <summary>
    /// Pixel patterns for edge case testing. Each pattern defines how to generate BGRA pixel data
    /// given a pixel index.
    /// </summary>
    public enum PixelPattern
    {
        /// <summary>
        /// Sequential values with variation across pixels: B=offset, G=offset+1, R=offset+2, A=offset+3.
        /// </summary>
        Sequential,

        /// <summary>
        /// All channels zero.
        /// </summary>
        AllZeros,

        /// <summary>
        /// All channels 0xFF.
        /// </summary>
        AllMax,

        /// <summary>
        /// Only the alpha channel is set (0xFF), RGB are zero.
        /// </summary>
        AlphaOnly,

        /// <summary>
        /// B=0x00, R=0xFF with full alpha — maximum contrast between B and R channels.
        /// </summary>
        MaxContrastBR,

        /// <summary>
        /// Alternating between two distinct pixel values per pixel index.
        /// </summary>
        Alternating,

        /// <summary>
        /// B=R, ensuring the swizzle still actually runs (detects no-op bugs).
        /// </summary>
        SymmetricBR,
    }

    /// <summary>
    /// Data lengths covering boundary conditions for scalar, Vector128 (16 bytes), and Vector256 (32 bytes) paths,
    /// including tail-handling edge cases. All lengths are multiples of 4 (one pixel = 4 bytes BGRA).
    /// </summary>
    private static readonly int[] test_lengths = Enumerable.Range(1, 512 / 4).Select(i => i * 4).ToArray();

    private static readonly PixelPattern[] pixel_patterns = Enum.GetValues<PixelPattern>();

    #region Test data generation

    private static (byte b, byte g, byte r, byte a) getPixel(PixelPattern pattern, int pixelIndex) => pattern switch
    {
        PixelPattern.Sequential => (
            (byte)(pixelIndex % 250),
            (byte)(pixelIndex % 250 + 1),
            (byte)(pixelIndex % 250 + 2),
            (byte)(pixelIndex % 250 + 3)),
        PixelPattern.AllZeros => (0, 0, 0, 0),
        PixelPattern.AllMax => (0xFF, 0xFF, 0xFF, 0xFF),
        PixelPattern.AlphaOnly => (0, 0, 0, 0xFF),
        PixelPattern.MaxContrastBR => (0x00, 0x80, 0xFF, 0xFF),
        PixelPattern.Alternating => pixelIndex % 2 == 0
            ? ((byte)0xAA, (byte)0xBB, (byte)0xCC, (byte)0xDD)
            : ((byte)0x11, (byte)0x22, (byte)0x33, (byte)0x44),
        PixelPattern.SymmetricBR => (0x42, 0x80, 0x42, 0xFF),
        _ => throw new ArgumentOutOfRangeException(nameof(pattern)),
    };

    /// <summary>
    /// Creates BGRA source data and the expected RGBA result for a given length and pixel pattern.
    /// </summary>
    private static (byte[] source, byte[] expected) createTestData(int length, PixelPattern pattern)
    {
        byte[] source = new byte[length];
        byte[] expected = new byte[length];

        for (int i = 0; i < length; i += 4)
        {
            var (b, g, r, a) = getPixel(pattern, i / 4);

            // Source is BGRA layout
            source[i] = b;
            source[i + 1] = g;
            source[i + 2] = r;
            source[i + 3] = a;

            // Expected is RGBA layout (B and R swapped)
            expected[i] = r;
            expected[i + 1] = g;
            expected[i + 2] = b;
            expected[i + 3] = a;
        }

        return (source, expected);
    }

    #endregion

    #region Test infrastructure

    private static unsafe void runSwizzleTest(Action<IntPtr, int> swizzleMethod, int length, PixelPattern pattern)
    {
        var (source, expected) = createTestData(length, pattern);

        fixed (byte* pSource = source)
        {
            swizzleMethod((IntPtr)pSource, source.Length);
        }

        Assert.That(source, Is.EqualTo(expected));
    }

    #endregion

    #region Per-method tests (length × pattern combinations)

    [Test]
    public void TestStableReference(
        [ValueSource(nameof(test_lengths))] int length,
        [ValueSource(nameof(pixel_patterns))] PixelPattern pattern)
        => runSwizzleTest(BitmapHelper.SwizzleToRgba32Scalar_osu_stable, length, pattern);

    [Test]
    public void TestScalar32(
        [ValueSource(nameof(test_lengths))] int length,
        [ValueSource(nameof(pixel_patterns))] PixelPattern pattern)
        => runSwizzleTest(BitmapHelper.SwizzleToRgba32Scalar, length, pattern);

    [Test]
    public void TestScalar64(
        [ValueSource(nameof(test_lengths))] int length,
        [ValueSource(nameof(pixel_patterns))] PixelPattern pattern)
        => runSwizzleTest(BitmapHelper.SwizzleToRgba32Scalar_64, length, pattern);

    [Test]
    public void TestVector128(
        [ValueSource(nameof(test_lengths))] int length,
        [ValueSource(nameof(pixel_patterns))] PixelPattern pattern)
    {
        if (!Vector128.IsHardwareAccelerated)
            Assert.Ignore("Vector128 hardware acceleration not supported on this platform.");

        runSwizzleTest(BitmapHelper.SwizzleToRgba32Vector128, length, pattern);
    }

    [Test]
    public void TestVector256(
        [ValueSource(nameof(test_lengths))] int length,
        [ValueSource(nameof(pixel_patterns))] PixelPattern pattern)
    {
        if (!Vector256.IsHardwareAccelerated)
            Assert.Ignore("Vector256 hardware acceleration not supported on this platform.");

        runSwizzleTest(BitmapHelper.SwizzleToRgba32Vector256, length, pattern);
    }

    #endregion
}
