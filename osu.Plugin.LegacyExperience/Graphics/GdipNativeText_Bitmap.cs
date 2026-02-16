using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp.PixelFormats;
using OpenTkPixelFormat = osuTK.Graphics.ES30.PixelFormat;

namespace osu.Plugin.LegacyExperience.Graphics;

partial class GdipNativeText
{
    internal partial class BitmapTextureUpload : ITextureUpload
    {
        private readonly Bitmap bitmap;
        private readonly BitmapData bitmapData;

        public BitmapTextureUpload(Bitmap bitmap)
        {
            this.bitmap = bitmap;
            this.bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);

            BitmapHelper.SwizzleToRgba32(bitmapData);
        }

        public unsafe ReadOnlySpan<Rgba32> Data => new ReadOnlySpan<Rgba32>(bitmapData.Scan0.ToPointer(), bitmapData.Stride * bitmapData.Height / Unsafe.SizeOf<Rgba32>());

        public int Level { get; set; }

        public RectangleI Bounds { get; set; }

        OpenTkPixelFormat ITextureUpload.Format => OpenTkPixelFormat.Rgba;

        public void Dispose()
        {
            bitmap.UnlockBits(bitmapData);
            bitmap.Dispose();
        }
    }
}
