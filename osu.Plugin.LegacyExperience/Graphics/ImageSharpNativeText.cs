using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// Provides functionality to render text using stable's font rendering system with ImageSharp backend.
/// Note that this class renders text to textures offscreen with CPU, which may be less performant than
/// using a GPU-based text rendering approach.
/// </summary>
public partial class ImageSharpNativeText : NativeTextBase
{
    private readonly IBindable<DisplayMode> currentDisplayMode = new Bindable<DisplayMode>();

    [BackgroundDependencyLoader]
    private void load(GameHost host)
    {
        currentDisplayMode.BindTo(host.Window.CurrentDisplayMode);
    }

    private static readonly SolidBrush DrawBrush = Brushes.Solid(Color.White);

    private readonly FontCollection fontCollection = new FontCollection();
    private readonly Dictionary<FontCacheKey, Font> fontCache = new();

    /// <summary>
    /// Called when a font is loaded from osu!ui.dll.
    /// </summary>
    protected override void AddFont(string resourceName, MemoryStream fontStream)
    {
        fontCollection.Add(fontStream);
        Logger.Log($"Loaded font {resourceName} to ImageSharp FontCollection", LoggingTarget.Runtime, LogLevel.Verbose);
    }

    /// <summary>
    /// Gets or creates a font from the cache.
    /// </summary>
    private Font? GetOrCreateFont(string fontName, float size, FontStyle style)
    {
        var key = new FontCacheKey(fontName, size, style);

        if (fontCache.TryGetValue(key, out Font? cached))
            return cached;

        FontFamily family;

        if (!fontCollection.TryGet(fontName, out family)
            && !SystemFonts.TryGet(fontName, out family))
        {
            // System.Drawing's Font fallback to call GdipGetGenericFontFamilySansSerif when the specified font is not found, 
            // so we do the same to mimic stable's behaviour.
            var fallback = msSansSerifFamily
                // osu!ui's fonts are generally less complete, so we prefer system font fallbacks over osu!ui's when the specified font is not found.
                ?? fallbackFontFamilies.Select(toNullable).FirstOrDefault()
                ?? fontCollection.Families.Select(toNullable).FirstOrDefault()
                ?? SystemFonts.Families.Select(toNullable).FirstOrDefault();

            if (!fallback.HasValue)
                return null;

            family = fallback.Value;
        }

        static T? toNullable<T>(T value) where T : struct => value;

        Font font = family.CreateFont(size, style);
        fontCache[key] = font;
        return font;
    }

    /// <summary>
    /// Builds the combined <see cref="FontStyle"/> flags from individual boolean parameters.
    /// </summary>
    private static FontStyle BuildFontStyle(bool bold, bool italic)
    {
        FontStyle style = FontStyle.Regular;

        if (bold) style |= FontStyle.Bold;
        if (italic) style |= FontStyle.Italic;

        return style;
    }

    private static readonly string[] fallbackFontNames =
    [
        // System.Drawing fallback to Microsoft Sans Serif (GdipGetGenericFontFamilySansSerif) first when the specified font is not found, 
        // so we put it at the front of the list to mimic stable's behaviour.
        "Microsoft Sans Serif",

        // dictionary order, this should match GDI+ DrawString's fallback order when the specified font doesn't support certain characters.
        ..new string[]
        {
            // we may want to add more fallbacks in the future if we find more missing characters.
            "Malgun Gothic",
            "Meiryo",
            "Microsoft YaHei",
            "Microsoft JhengHei",
            "Segoe UI",
            "Tahoma",
        }.Order(StringComparer.InvariantCulture)
    ];

    private static readonly FontFamily[] fallbackFontFamilies = fallbackFontNames.Select(static name =>
    {
        if (SystemFonts.TryGet(name, out var family))
            return family;

        Logger.Log($"Fallback font '{name}' not found in system fonts.", LoggingTarget.Runtime, LogLevel.Verbose);
        return default;
    }).Where(static f => f != default).ToArray();

    private static readonly FontFamily? msSansSerifFamily = fallbackFontFamilies.FirstOrDefault() is FontFamily family
        && family.Name == "Microsoft Sans Serif" ? family : null;

    private record struct FontCacheKey(string Name, float Size, FontStyle Style);

    private const float target_dpi = 96f;

    /// <summary>
    /// ImageSharp takes a relatively long time to collect fonts during its first font rendering (primarily due to TextMeasurer, ~2s).
    /// To avoid game freezes when entering main menu, we trigger font collection asynchronously during loading.
    /// </summary>
    public void Warmup()
    {
        CreateText(new NativeText.TextCreationParameters
        {
            Text = "Load", // must be non-null and non-empty to trigger font collection.
            Size = 14,
            FontFace = NativeText.LegacyFontFace.DefaultRegular,
            RenderFlags = NativeText.TextRenderFlags.MeasureUnrestrictedBounds,
        }, out _);
    }

    /// <summary>
    /// Creates a texture containing the rendered text using ImageSharp backend.
    /// </summary>
    /// <param name="parameters">The text creation parameters.</param>
    /// <param name="result">The result of the text creation operation.</param>
    /// <returns>The created texture, or null if text is empty or could not be rendered.</returns>
    public override void CreateText(in NativeText.TextCreationParameters parameters, out NativeText.TextCreationResult result)
    {
        result = new NativeText.TextCreationResult
        {
            RequestedRestrictBounds = parameters.RestrictBounds,
        };

        ReadOnlyMemory<char> textMemory = parameters.Text.AsMemory();

        if (textMemory.IsEmpty)
            return;

        // TODO: SDL3 is removing SDL_GetDisplayDPI, we need to find an GdipGetDpiX equivalent way to for stable-like DPI scaling in the future.
        if (SDL2.SDL.SDL_GetDisplayDPI(currentDisplayMode.Value.DisplayIndex, out _, out float dpiX, out _) is not 0)
        {
            Logger.Log($"Failed to get display DPI for display index {currentDisplayMode.Value.DisplayIndex}. SDL Error: {SDL2.SDL.SDL_GetError()}", LoggingTarget.Runtime, LogLevel.Verbose);
            dpiX = target_dpi;
        }

        float dpiRatio = dpiX / target_dpi;

        float fontSize = parameters.Size * 1.03f; // magic ratio to compensate for stable's slightly larger font rendering, especially at smaller sizes. This is not a perfect solution but it should be good enough for now.

        Vector2 restrictBounds = parameters.RestrictBounds * dpiRatio;

        string fontName = ResolveFontName(parameters.FontFace, textMemory.Span, fontSize, parameters.Bold);
        FontStyle fontStyle = BuildFontStyle(parameters.Bold, parameters.Italic);
        Font? font = GetOrCreateFont(fontName, fontSize, fontStyle);

        // The system doesn't install any font, and we failed to load osu!ui.dll, 
        // so we have no choice but to give up rendering text.
        if (font is null)
            return;

        // stable behaviour
        ProcessFontSpecificGlyphAdjustments(ref textMemory, fontName);

        var textOptions = new RichTextOptions(font)
        {
            Dpi = dpiX,
            HorizontalAlignment = mapAlignment(parameters.Alignment),
            WordBreaking = WordBreaking.BreakWord,
            // keep this list small, as each fallback adds (much) to processing time.
            FallbackFontFamilies = fallbackFontFamilies,
            // GDI+ leaves more space between lines than ImageSharp's default, we need to increase line spacing to compensate for that.
            LineSpacing = 1.25f,
        };

        FontRectangle bounds = default;

        if (parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.MeasureUnrestrictedBounds))
        {
            // Measure bounds without wrapping to get the unrestricted size, which is needed to determine if the text was actually restricted or not.
            textOptions.WrappingLength = -1;
            var unrestrictedBounds = TextMeasurer.MeasureAdvance(textMemory.Span, textOptions);

            result = result with
            {
                UnrestrictedBoundsSize = new Vector2(unrestrictedBounds.Right, unrestrictedBounds.Bottom),
            };
        }

        bool doRender = parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.Render);

        // measure restricted bounds later to keep WrappingLength intact for rendering
        if (doRender || parameters.RenderFlags.HasFlagFast(NativeText.TextRenderFlags.MeasureBounds))
        {
            textOptions.WrappingLength = restrictBounds.X > 0
                ? (int)restrictBounds.X
                : -1;

            // use advance here to leave full height for text, making text centered vertically.
            bounds = TextMeasurer.MeasureAdvance(textMemory.Span, textOptions);

            result = result with
            {
                BoundsSize = new Vector2(bounds.Right, bounds.Bottom),
            };
        }

        if (!doRender)
            return;

        // no need to create an empty texture
        if (bounds.IsEmpty)
            return;

        int width = (int)MathF.Ceiling(bounds.Right);
        int height = (int)MathF.Ceiling(bounds.Bottom);

        // we try to draw one more pixel to avoid 1px gap issues, masking can be used to crop later if needed.
        if (restrictBounds.Y > 0)
            height = Math.Min(height, (int)MathF.Ceiling(restrictBounds.Y));

        if (restrictBounds.X > 0)
            width = Math.Min(width, (int)MathF.Ceiling(restrictBounds.X));

        if (width <= 0 || height <= 0)
            return;

        var image = new Image<Rgba32>(width, height);

        // ImageSharp's DrawText doesn't support ReadOnlyMemory<char> for some reason.
        // However, ReadOnlyMemory<char> returns original string internally if we are not modifying the text for font-specific glyph adjustments
        var textString = textMemory.ToString();

        image.Mutate(ctx =>
        {
            ctx.DrawText(textOptions, textString, DrawBrush, null);
        });

        var texture = CreateTexture(image.Width, image.Height);
        texture.ScaleAdjust = dpiRatio;
        texture.SetData(new TextureUpload(image));

        result = result with
        {
            Texture = texture,
        };
    }

    /// <summary>
    /// Maps <see cref="NativeText.TextAlignment"/> to SixLabors <see cref="HorizontalAlignment"/>.
    /// </summary>
    private static HorizontalAlignment mapAlignment(NativeText.TextAlignment alignment)
    {
        return alignment switch
        {
            NativeText.TextAlignment.Left or NativeText.TextAlignment.LeftFixed => HorizontalAlignment.Left,
            NativeText.TextAlignment.Centre => HorizontalAlignment.Center,
            NativeText.TextAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
    }
}
