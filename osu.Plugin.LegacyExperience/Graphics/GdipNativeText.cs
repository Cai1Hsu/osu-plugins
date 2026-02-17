using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using osu.Framework.Allocation;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Logging;
using osuTK;
using GdipGraphics = System.Drawing.Graphics;
using GdipPixelFormat = System.Drawing.Imaging.PixelFormat;
using GdipFontStyle = System.Drawing.FontStyle;
using GdipFont = System.Drawing.Font;
using GdipFontFamily = System.Drawing.FontFamily;
using GdipBitmap = System.Drawing.Bitmap;
using GdipRectangleF = System.Drawing.RectangleF;
using GdipSizeF = System.Drawing.SizeF;
using osu.Framework.Platform;

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// Provides functionality to render text using stable's font rendering system with GDI+ backend.
/// This implementation uses GDI+ for text measurement and rendering, which may be more compatible
/// with stable's text rendering behavior.
/// </summary>
[SupportedOSPlatform("windows")]
public partial class GdipNativeText : NativeTextBase
{
    private GdipGraphics wndGraphics = null!;
    private readonly PrivateFontCollection fontCollection = new();

    private Storage fontCacheStorage = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        wndGraphics = GdipGraphics.FromHwnd(IntPtr.Zero);
    }

    protected override void AddFont(string resourceName, MemoryStream fontStream)
    {
        // Don't use GetBuffer, it returns the whole byte array,
        // but in our case, the font data is the subset of the array (starting after the length prefix).
        if (!fontStream.TryGetBuffer(out var fontData))
            fontData = fontStream.ToArray();

        // This method is call before load, so DI doesn't work for us.
        fontCacheStorage ??= LazerStorage.GetStorageForDirectory("font-cache");

        if (!fontCacheStorage.Exists(resourceName))
        {
            using (var fs = fontCacheStorage.CreateFileSafely(resourceName))
                fs.Write(fontData);
        }

        try
        {
            // There's a weird quirk in GDI+ where it always selects random font style if you uses AddMemoryFont.
            // This thread (https://stackoverflow.com/questions/31140819/privatefontcollection-with-gdi-sometimes-uses-the-wrong-fontstyle-in-windows-8)
            // suggests that you use a single PrivateFontCollection for the each font family, 
            // which is the osu!stable approach. But it doesn't work in our case for some reason.
            // And this thread (https://stackoverflow.com/questions/25583394/privatefontcollection-addmemoryfont-producing-random-errors-on-windows-server-20)
            // suggests that AddFontFile doesn't have this issue.
            // I checked libgdiplus's source and comments there strongly against you use GdipPrivateAddMemoryFont due to various issues, so let's just use AddFontFile for now.
            // Also, GdipPrivateAddMemoryFont just internally store your font data in a temporary file and call AddFontFile, so we might as well skip the middleman.
            fontCollection.AddFontFile(fontCacheStorage.GetFullPath(resourceName));
            Logger.Log($"Loaded font {resourceName} to GDI+ PrivateFontCollection", LoggingTarget.Runtime, LogLevel.Verbose);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to load font {resourceName} to GDI+ PrivateFontCollection: {e.Message}", LoggingTarget.Runtime, LogLevel.Error);
        }
    }

    private const float target_dpi = 96f;

    private readonly record struct FontCacheKey(string Name, float Size, GdipFontStyle Style);

    private readonly Dictionary<FontCacheKey, GdipFont> fontCache = new();

    /// <summary>
    /// Gets a font with the specified properties, trying the private collection first.
    /// </summary>
    private GdipFont GetFont(string fontName, float size, GdipFontStyle style)
    {
        var cacheKey = new FontCacheKey(fontName, size, style);
        if (fontCache.TryGetValue(cacheKey, out var cachedFont))
            return cachedFont;

        GdipFontFamily? family = null;

        foreach (var f in fontCollection.Families)
        {
            if (f.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase)
                && f.IsStyleAvailable(style))
            {
                family = f;
            }
        }

        var font = family is not null
            ? new GdipFont(family, size, style)
            // GDI+ fallbacks internally, so we can just try to create the font directly by name.
            : new GdipFont(fontName, size, style);

        return fontCache[cacheKey] = font;
    }

    /// <summary>
    /// Creates a texture containing the rendered text using GDI+ backend.
    /// </summary>
    /// <param name="parameters">The text creation parameters.</param>
    /// <param name="result">The result of the text creation operation.</param>
    public override void CreateText(in NativeText.TextCreationParameters parameters, out NativeText.TextCreationResult result)
    {
        result = new NativeText.TextCreationResult
        {
            RequestedRestrictBounds = parameters.RestrictBounds,
        };

        string text = parameters.Text;

        if (string.IsNullOrEmpty(text))
            return;

        float dpiX = wndGraphics.DpiX;

        float dpiRatio = dpiX / target_dpi;

        float fontSize = parameters.Size;

        Vector2 restrictBounds = parameters.RestrictBounds * dpiRatio;

        string fontName = ResolveFontName(parameters.FontFace, text.AsSpan(), fontSize, parameters.Bold);

        GdipFontStyle style = GdipFontStyle.Regular;
        if (parameters.Bold) style |= GdipFontStyle.Bold;
        if (parameters.Italic) style |= GdipFontStyle.Italic;

        GdipFont? gdipFont = GetFont(fontName, fontSize, style);

        // Apply font-specific glyph adjustments
        var textMemory = text.AsMemory();
        ProcessFontSpecificGlyphAdjustments(ref textMemory, fontName);
        var adjustedText = textMemory.ToString();

        // Set up text formatting options
        using var stringFormat = new StringFormat()
        {
            Alignment = mapTextAlignment(parameters.Alignment),
            LineAlignment = StringAlignment.Near,
        };

        GdipSizeF unrestrictedSize = default;

        if (parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.MeasureUnrestrictedBounds)
            || restrictBounds == Vector2.Zero)
        {
            // stable doesn't pass stringformat to unstricted measurement.
            GdipSizeF measuredSize = wndGraphics.MeasureString(adjustedText, gdipFont);
            unrestrictedSize = measuredSize;

            result = result with
            {
                UnrestrictedBoundsSize = new Vector2(measuredSize.Width, measuredSize.Height),
            };
        }

        bool doRender = parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.Render);

        // Measure with restriction if needed
        GdipSizeF drawSize = default;

        if (doRender || parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.MeasureBounds))
        {
            if (restrictBounds != Vector2.Zero)
                drawSize = wndGraphics.MeasureString(adjustedText, gdipFont, new GdipSizeF(restrictBounds.X, restrictBounds.Y), stringFormat);
            else
                drawSize = unrestrictedSize;

            result = result with
            {
                BoundsSize = new Vector2(drawSize.Width, drawSize.Height),
            };
        }

        if (!doRender)
            return;

        // Calculate final texture size
        int width = (int)MathF.Ceiling(drawSize.Width);
        int height = (int)MathF.Ceiling(drawSize.Height);

        if (restrictBounds.Y > 0)
            height = Math.Min(height, (int)MathF.Ceiling(restrictBounds.Y));

        if (restrictBounds.X > 0)
            width = Math.Min(width, (int)MathF.Ceiling(restrictBounds.X));

        if (width <= 0 || height <= 0)
            return;

        GdipBitmap? bitmap = null;

        try
        {
            bitmap = new GdipBitmap(width, height, GdipPixelFormat.Format32bppArgb);
            using (var gfx = GdipGraphics.FromImage(bitmap))
            {
                gfx.TextRenderingHint = TextRenderingHint.AntiAlias;
                gfx.SmoothingMode = SmoothingMode.HighQuality;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;

                if (restrictBounds != Vector2.Zero)
                    gfx.DrawString(adjustedText, gdipFont, Brushes.White, new GdipRectangleF(0, 0, restrictBounds.X, restrictBounds.Y), stringFormat);
                else
                    gfx.DrawString(adjustedText, gdipFont, Brushes.White, 0, 0);
            }

            var texture = CreateTexture(width, height);
            texture.ScaleAdjust = dpiRatio;
            texture.SetData(new BitmapTextureUpload(bitmap));

            bitmap = null; // BitmapTextureUpload manages the bitmap's memory, so we should not dispose it after this point.

            result = result with
            {
                Texture = texture,
            };
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    /// <summary>
    /// Converts NativeText.TextAlignment to System.Drawing StringAlignment.
    /// </summary>
    private static StringAlignment mapTextAlignment(NativeText.TextAlignment alignment)
    {
        return alignment switch
        {
            NativeText.TextAlignment.Left or NativeText.TextAlignment.LeftFixed => StringAlignment.Near,
            NativeText.TextAlignment.Centre => StringAlignment.Center,
            NativeText.TextAlignment.Right => StringAlignment.Far,
            _ => StringAlignment.Near,
        };
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        // null check here in case load is not called
        wndGraphics?.Dispose();

        foreach (var font in fontCache.Values)
            font.Dispose();

        fontCache.Clear();
        fontCollection.Dispose();
    }
}
