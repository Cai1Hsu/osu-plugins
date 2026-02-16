using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
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

    [BackgroundDependencyLoader]
    private void load()
    {
        wndGraphics = GdipGraphics.FromHwnd(IntPtr.Zero);
    }

    protected unsafe override void AddFont(string resourceName, MemoryStream fontStream)
    {
        // Don't use GetBuffer, it returns the whole byte array,
        // but in our case, the font data is the subset of the array (starting after the length prefix).
        if (!fontStream.TryGetBuffer(out var fontData))
            fontData = fontStream.ToArray();

        IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Count);
        try
        {
            var destSpan = new Span<byte>(fontPtr.ToPointer(), fontData.Count);
            fontData.AsSpan().CopyTo(destSpan);
            fontCollection.AddMemoryFont(fontPtr, fontData.Count);
            Logger.Log($"Loaded font {resourceName} to GDI+ PrivateFontCollection", LoggingTarget.Runtime, LogLevel.Verbose);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to load font {resourceName} to GDI+ PrivateFontCollection: {e.Message}", LoggingTarget.Runtime, LogLevel.Error);
        }
        finally
        {
            Marshal.FreeCoTaskMem(fontPtr);
        }
    }

    private const float target_dpi = 96f;

    /// <summary>
    /// Gets a font with the specified properties, trying the private collection first.
    /// </summary>
    private GdipFont GetFont(string fontName, float size, GdipFontStyle style)
    {
        // Check if the font is in our private collection
        foreach (var family in fontCollection.Families)
        {
            if (family.Name == fontName)
            {
                return new GdipFont(family, size, style, GraphicsUnit.Pixel);
            }
        }

        // Fallback to system fonts
        try
        {
            return new GdipFont(fontName, size, style, GraphicsUnit.Pixel);
        }
        catch
        {
            // Fallback generic
            try
            {
                return new GdipFont(GdipFontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
            }
            catch
            {
                // Last ditch effort
                return new GdipFont("Arial", size, style, GraphicsUnit.Pixel);
            }
        }
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

        GdipFont? gdipFont;
        try
        {
            gdipFont = GetFont(fontName, fontSize, style);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to create font {fontName}: {e.Message}", LoggingTarget.Runtime, LogLevel.Error);
            return;
        }

        // Apply font-specific glyph adjustments
        var textMemory = text.AsMemory();
        ProcessFontSpecificGlyphAdjustments(ref textMemory, fontName);
        var adjustedText = textMemory.ToString();

        // Set up text formatting options
        var stringFormat = new StringFormat()
        {
            Alignment = mapTextAlignment(parameters.Alignment),
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.None,
        };

        // Measure text
        GdipSizeF measuredSize = wndGraphics.MeasureString(adjustedText, gdipFont, int.MaxValue, stringFormat);

        if (parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.MeasureUnrestrictedBounds))
        {
            result = result with
            {
                UnrestrictedBoundsSize = new Vector2(measuredSize.Width, measuredSize.Height),
            };
        }

        bool doRender = parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.Render);

        if (doRender || parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.MeasureBounds))
        {
            // Measure with restriction if needed
            GdipSizeF restrictedSize = measuredSize;

            if (restrictBounds.X > 0)
            {
                restrictedSize = wndGraphics.MeasureString(adjustedText, gdipFont, (int)restrictBounds.X, stringFormat);
            }

            result = result with
            {
                BoundsSize = new Vector2(restrictedSize.Width, restrictedSize.Height),
            };
        }

        if (!doRender)
        {
            gdipFont?.Dispose();
            stringFormat?.Dispose();
            return;
        }

        // Calculate final texture size
        int width = (int)MathF.Ceiling(measuredSize.Width);
        int height = (int)MathF.Ceiling(measuredSize.Height);

        if (restrictBounds.Y > 0)
            height = Math.Min(height, (int)MathF.Ceiling(restrictBounds.Y));

        if (restrictBounds.X > 0)
            width = Math.Min(width, (int)MathF.Ceiling(restrictBounds.X));

        if (width <= 0 || height <= 0)
        {
            gdipFont?.Dispose();
            stringFormat?.Dispose();
            return;
        }

        // stable uses Format32bppArgb, but here we need premultiplied alpha for correct blending.
        var bitmap = new GdipBitmap(width, height, GdipPixelFormat.Format32bppPArgb);
        using (var gfx = GdipGraphics.FromImage(bitmap))
        {
            gfx.TextRenderingHint = TextRenderingHint.AntiAlias;
            gfx.SmoothingMode = SmoothingMode.HighQuality;
            gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gfx.DrawString(adjustedText, gdipFont, Brushes.White, new GdipRectangleF(0, 0, width, height), stringFormat);
        }

        var texture = CreateTexture(width, height);
        texture.ScaleAdjust = dpiRatio;
        texture.SetData(new BitmapTextureUpload(bitmap));

        result = result with
        {
            Texture = texture,
        };

        gdipFont.Dispose();
        stringFormat.Dispose();
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
}
