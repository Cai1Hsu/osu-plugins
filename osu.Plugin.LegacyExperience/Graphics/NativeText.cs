using System.Reflection;
using System.Resources;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osuTK;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// Provides functionality to render text using stable's font rendering system (NativeText).
/// Note that this class renders text to textures offscreen with CPU, which may be less performant than
/// using a GPU-based text rendering approach.
/// </summary>
public partial class NativeText : Component
{
    private Bindable<string> frameworkLocale = null!;

    private TextureAtlas textureAtlas = null!;

    [Resolved]
    private IRenderer renderer { get; set; } = null!;

    // of's TextureStore limits texture size to 1024 due to mipmapping's performance impact,
    // but we are not mipmapping for text textures, so we can use larger textures to reduce atlas usage and improve performance.
    private const int max_atlas_size = 4096;

    [BackgroundDependencyLoader]
    private void load(FrameworkConfigManager frameworkConfig)
    {
        frameworkLocale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);

        int atlasSize = Math.Min(renderer.MaxTextureSize, max_atlas_size);
        textureAtlas = new TextureAtlas(renderer, atlasSize, atlasSize, manualMipmaps: true);

        loadOsuUI();
        populateFontCollection();
    }

    [Resolved]
    private OsuGame? osuGame { get; set; }

    [Resolved]
    private LegacyImportManager? legacyImportManager { get; set; }

    [Resolved]
    private Storage lazerStorage { get; set; } = null!;

    [Resolved]
    private INotificationOverlay? notificationOverlay { get; set; }

    private Assembly? osu_ui_Assembly;

    /// <summary>
    /// Whether osu!ui.dll has been successfully loaded.
    /// Note that the fonts may still be usable even if this is false, such as when loading from system fonts.
    /// </summary>
    public bool IsOsuUILoaded => osu_ui_Assembly is not null;

    private void loadOsuUI()
    {
        var fallbackStorages = new[]
        {
            legacyImportManager?.GetCurrentStableStorage(),
            osuGame?.GetStorageForStableInstall(),
            // osu actually has a registry entry for stable installs, but we want to avoid platform-specific code if possible.
            // so let's try the default installation path(~\AppData\Local\osu!) for now.
            new NativeStorage(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!")),
            lazerStorage,
            // also attempt the directory of our assembly as a last resort.
            new NativeStorage(Path.GetDirectoryName(typeof(NativeText).Assembly.Location) ?? string.Empty),
        };

        foreach (var storage in fallbackStorages)
        {
            if (storage is null)
                continue;

            osu_ui_Assembly = tryLoadOsuUIAssembly(storage);

            if (osu_ui_Assembly is not null)
                break;
        }

        if (osu_ui_Assembly is null)
        {
            string message = "You must provide a valid stable installation to render stable-style text. " +
                             "Consider either setting a valid stable installation path in lazer's migration settings or " +
                             "copy osu!ui.dll into the game's data directory. " +
                             "Note that a restart is required for changes to take effect.";

            if (notificationOverlay is not null)
            {
                notificationOverlay.Post(new SimpleNotification
                {
                    Text = message,
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                    IconColour = Colour4.Red,
                });
            }
            else
            {
                Logger.Log(message, LoggingTarget.Runtime, LogLevel.Error);
            }
        }
    }

    /// <summary>
    /// Attempts to load osu!ui.dll if it has not already been loaded.
    /// </summary>
    public void TryLoadOsuUI()
    {
        if (osu_ui_Assembly is not null)
            return;

        // font collection uses hashset internally, so re-adding fonts is safe.
        loadOsuUI();
        populateFontCollection();
    }

    private readonly FontCollection fontCollection = new FontCollection();
    private readonly Dictionary<FontCacheKey, Font> fontCache = new();

    private const string osu_ui_dll = "osu!ui.dll";

    private Assembly? tryLoadOsuUIAssembly(Storage possibleStorage)
    {
        if (!possibleStorage.Exists(osu_ui_dll))
            return null;

        var assemblyPath = possibleStorage.GetFullPath(osu_ui_dll);

        try
        {
            Logger.Log($"Attempting to load {osu_ui_dll} from {assemblyPath}", LoggingTarget.Runtime, LogLevel.Verbose);

            return Assembly.LoadFrom(assemblyPath);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to load {osu_ui_dll} from {assemblyPath}. Exception: {e.Message}", LoggingTarget.Runtime, LogLevel.Verbose);
            return null;
        }
    }

    private void populateFontCollection()
    {
        ReadOnlySpan<byte> ttfHeader = [0x00, 0x01, 0x00, 0x00, 0x00];
        ReadOnlySpan<byte> otfHeader = [0x4F, 0x54, 0x54, 0x4F]; // "OTTO"

        var resourceStream = osu_ui_Assembly?.GetManifestResourceStream("osu_ui.ResourcesStore.resources");

        if (resourceStream is null)
            return;

        Span<byte> lengthSpan = stackalloc byte[sizeof(int)];

        // the idiot microsoft knows that ResourceReader is unsafe due to binary serialization vulnerabilities
        // but they NEVER provide a safe alternative for reading embedded resource names WITHOUT serializing them.
        // We have to do this reflection hack to read resource names safely(AND WITHOUT TRIGGERING THOSE FUCKING EXCEPTIONS).
        using (var reader = new ResourceReader(resourceStream))
        using (var enumerator = new ResourceEnumerator(reader))
        {
            while (enumerator.MoveNext())
            {
                string resourceName = enumerator.Current;

                reader.GetResourceData(resourceName, out string resourceType, out byte[] resourceData);

                if (resourceType != "ResourceTypeCode.ByteArray" || resourceData.Length < 9)
                    continue;

                resourceData.AsSpan(0, sizeof(int)).CopyTo(lengthSpan);

                if (!BitConverter.IsLittleEndian)
                    lengthSpan.Reverse();

                // performs a simple binary serialization for byte[]
                var length = BitConverter.ToInt32(lengthSpan);
                var dataSpan = resourceData.AsSpan(sizeof(int));

                if (dataSpan.Length != length)
                    continue;

                if (!dataSpan[0..5].SequenceEqual(ttfHeader) &&
                    !dataSpan[0..4].SequenceEqual(otfHeader))
                    continue;

                using var fontStream = new MemoryStream(resourceData, sizeof(int), length, false);
                fontCollection.Add(fontStream);

                Logger.Log($"Loaded font {resourceName} from {osu_ui_dll}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
        }
    }

    private static readonly SolidBrush DrawBrush = Brushes.Solid(Color.White);

    private void processFontSpecificGlyphAdjustments(ref ReadOnlyMemory<char> text, string fontName)
    {
        // stable's "Aller" font has a custom glyph for digits that are mapped to the Unicode private use area.
        if (fontName.StartsWith("Aller"))
        {
            char[]? chars = null;

            var textSpan = text.Span;
            for (int i = 0; i < textSpan.Length; i++)
            {
                char c = textSpan[i];

                if (c is >= '0' and <= '9')
                {
                    chars ??= textSpan.ToArray();
                    chars[i] = (char)(c + 63500);
                }
            }

            if (chars is not null)
                text = chars;
        }
    }

    /// <summary>
    /// Creates a texture containing the rendered text based on the provided parameters.
    /// </summary>
    /// <param name="parameters">The text creation parameters.</param>
    /// <param name="result">The result of the text creation operation.</param>
    /// <returns>The created texture, or null if text is empty or could not be rendered.</returns>
    public void CreateText(in TextCreationParameters parameters, out TextCreationResult result)
    {
        result = new TextCreationResult
        {
            RequestedRestrictBounds = parameters.RestrictBounds,
        };

        ReadOnlyMemory<char> textMemory = parameters.Text.AsMemory();

        if (textMemory.IsEmpty)
            return;

        string fontName = selectFontFamily(parameters);

        FontStyle fontStyle = BuildFontStyle(parameters.Bold, parameters.Italic);
        Font? font = getOrCreateFont(fontName, parameters.Size, fontStyle);

        // The system doesn't install any font, and we failed to load osu!ui.dll, 
        // so we have no choice but to give up rendering text.
        if (font is null)
            return;

        // stable behaviour
        processFontSpecificGlyphAdjustments(ref textMemory, fontName);

        var textOptions = new RichTextOptions(font)
        {
            Dpi = parameters.Dpi,
            HorizontalAlignment = mapAlignment(parameters.Alignment),
            WordBreaking = WordBreaking.BreakAll,
            // keep this list small, as each fallback adds (much) to processing time.
            FallbackFontFamilies = fallbackFontFamilies,
        };

        FontRectangle bounds = default;

        if (parameters.RenderFlags.HasFlagFast(TextRenderFlags.MeasureUnrestrictedBounds))
        {
            // Measure bounds without wrapping to get the unrestricted size, which is needed to determine if the text was actually restricted or not.
            textOptions.WrappingLength = -1;
            var unrestrictedBounds = TextMeasurer.MeasureBounds(textMemory.Span, textOptions);

            result = result with
            {
                UnrestrictedBoundsSize = new Vector2(unrestrictedBounds.Right, unrestrictedBounds.Bottom),
            };
        }

        bool doRender = parameters.RenderFlags.HasFlagFast(TextRenderFlags.Render);

        // measure restricted bounds later to keep WrappingLength intact for rendering
        if (doRender || parameters.RenderFlags.HasFlagFast(TextRenderFlags.MeasureBounds))
        {
            textOptions.WrappingLength = parameters.RestrictBounds.X > 0
                ? (int)parameters.RestrictBounds.X
                : -1;
            bounds = TextMeasurer.MeasureBounds(textMemory.Span, textOptions);

            result = result with
            {
                BoundsSize = new Vector2(bounds.Right, bounds.Bottom),
            };
        }

        if (!doRender)
            return;

        // no need to create a empty texture
        if (bounds.IsEmpty)
            return;

        int width = (int)MathF.Ceiling(bounds.Right);
        int height = (int)MathF.Ceiling(bounds.Bottom);

        // we try to draw one more pixel to avoid 1px gap issues, masking can be used to crop later if needed.
        if (parameters.RestrictBounds.Y > 0)
            height = Math.Min(height, (int)MathF.Ceiling(parameters.RestrictBounds.Y));

        if (parameters.RestrictBounds.X > 0)
            width = Math.Min(width, (int)MathF.Ceiling(parameters.RestrictBounds.X));

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

        var texture = textureAtlas.Add(image.Width, image.Height)
            ?? renderer.CreateTexture(image.Width, image.Height);
        texture.SetData(new TextureUpload(image));

        result = result with
        {
            Texture = texture,
        };
    }

    private string selectFontFamily(in TextCreationParameters parameters)
    {
        return resolveFontName(parameters.FontFace, parameters.Text, parameters.Size, parameters.Bold);
    }

    private string resolveFontName(LegacyFontFace fontFace, string text, float size, bool bold)
    {
        string fontName = GetFontFace(fontFace);

        // stable's behaviour:
        if (fontFace is LegacyFontFace.DefaultRegular or LegacyFontFace.DefaultLight)
        {
            string? languageFont = getLanguageSpecificFont(text);

            if (languageFont != null)
                fontName = languageFont;
        }

        // Small sizes can't render Light variants well
        if (size < 20f && fontName.EndsWith(" Light"))
            fontName = fontName.Replace(" Light", string.Empty);

        // Bold overrides Light variant
        if (bold && fontName.EndsWith(" Light"))
            fontName = fontName.Replace(" Light", string.Empty);

        return fontName;
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

    /// <summary>
    /// Maps <see cref="TextAlignment"/> to SixLabors <see cref="HorizontalAlignment"/>.
    /// </summary>
    private static HorizontalAlignment mapAlignment(TextAlignment alignment)
    {
        return alignment switch
        {
            TextAlignment.Left or TextAlignment.LeftFixed => HorizontalAlignment.Left,
            TextAlignment.Centre => HorizontalAlignment.Center,
            TextAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
    }

    private Font? getOrCreateFont(string fontName, float size, FontStyle style)
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

    private readonly record struct FontCacheKey(string Name, float Size, FontStyle Style);

    /// <summary>
    /// Detects CJK or special script characters and returns an appropriate system font name.
    /// Ported from stable's getLanguageSpecificFont.
    /// </summary>
    private string? getLanguageSpecificFont(string text)
    {
        ScriptType script = detectScript(text);

        return script switch
        {
            ScriptType.Japanese => "Meiryo",
            ScriptType.Korean => "Malgun Gothic",
            ScriptType.Cyrillic => "Tahoma",
            ScriptType.CjkUnified => frameworkLocale.Value switch
            {
                "zh" => "Microsoft YaHei",
                "zh-tw" => "Microsoft JhengHei",
                "ja" => "Meiryo",
                "ko" => "Malgun Gothic",
                _ => "Segoe UI",
            },
            _ => null,
        };
    }

    /// <summary>
    /// Detects the primary script type of the text based on the first non-Latin character.
    /// Extracted from unicode.org scripts data, may differ slightly from stable's implementation.
    /// </summary>
    private static ScriptType detectScript(string text)
    {
        foreach (char c in text)
        {
            // Japanese: Hiragana + Katakana
            if (c is >= '\u3040' and <= '\u309F' or >= '\u30A0' and <= '\u30FF')
                return ScriptType.Japanese;

            // Korean: Hangul syllables + Jamo
            if (c is >= '\uAC00' and <= '\uD7AF' or >= '\u1100' and <= '\u11FF')
                return ScriptType.Korean;

            if (c is >= '\u0400' and <= '\u04FF' or // Cyrillic
                     >= '\u0500' and <= '\u052F' or // Cyrillic Supplement
                     >= '\u2DE0' and <= '\u2DFF' or // Cyrillic Extended-A
                     >= '\uA640' and <= '\uA69F' or // Cyrillic Extended-B
                     >= '\u1C80' and <= '\u1C8F' or // Cyrillic Extended-C
                                                    // Skipping Cyrillic Extended-D (U+1E030 to U+1E08F) as out of 16-bit char range
                     >= '\uFE2E' and <= '\uFE2F' || // Combining Half Marks
                     c is '\u1D2B' or '\u1D78') // Phonetic Extensions (Cyrillic chars only) 
                return ScriptType.Cyrillic;

            // CJK Unified Ideographs + Extension A
            if (c is >= '\u4E00' and <= '\u9FFF' or >= '\u3400' and <= '\u4DBF')
                return ScriptType.CjkUnified;
        }

        return ScriptType.Latin;
    }

    /// <summary>
    /// Maps <see cref="LegacyFontFace"/> enum to the actual font family name.
    /// </summary>
    internal static string GetFontFace(LegacyFontFace fontFace)
    {
        return fontFace switch
        {
            LegacyFontFace.DefaultRegular => "Aller",
            LegacyFontFace.Tahoma => "Tahoma",
            LegacyFontFace.FontAwesome => "FontAwesome",
            LegacyFontFace.Exo2 => "Exo 2.0",
            LegacyFontFace.Exo2Black => "Exo 2.0 Black",
            LegacyFontFace.Exo2ExtraBold => "Exo 2.0 Extra Bold",
            LegacyFontFace.Exo2ExtraLight => "Exo 2.0 Extra Light",
            LegacyFontFace.Exo2Light => "Exo 2.0 Light",
            LegacyFontFace.Exo2Medium => "Exo 2.0 Medium",
            LegacyFontFace.Exo2SemiBold => "Exo 2.0 Semi Bold",
            LegacyFontFace.Exo2Thin => "Exo 2.0 Thin",
            _ => "Aller Light",
        };
    }

    private enum ScriptType
    {
        Latin,
        Japanese,
        Korean,
        Cyrillic,
        CjkUnified,
    }

    public enum TextAlignment
    {
        Left = 0,
        LeftFixed = 1,
        Centre = 2,
        Right = 3,
    }

    public enum LegacyFontFace
    {
        DefaultLight = 0,
        DefaultRegular = 1,
        Tahoma = 2,
        FontAwesome = 3,
        Exo2 = 4,
        Exo2Black = 5,
        Exo2ExtraBold = 6,
        Exo2ExtraLight = 7,
        Exo2Light = 8,
        Exo2Medium = 9,
        Exo2SemiBold = 10,
        Exo2Thin = 11,
    }

    /// <summary>
    /// Parameters for creating text textures.
    /// </summary>
    public readonly struct TextCreationParameters
    {
        /// <summary>
        /// The text to be rendered.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// The font size.
        /// </summary>
        public required float Size { get; init; }

        /// <summary>
        /// The DPI (dots per inch) for rendering the text.
        /// </summary>
        public float Dpi { get; init; } = 72; // ImageSharp's default DPI

        /// <summary>
        /// The maximum bounds to restrict the text within.
        /// </summary>
        public Vector2 RestrictBounds { get; init; }

        /// <summary>
        /// The font face to be used.
        /// </summary>
        public LegacyFontFace FontFace { get; init; }

        /// <summary>
        /// Whether the text should be rendered in bold style.
        /// </summary>
        public bool Bold { get; init; }

        /// <summary>
        /// Whether the text should be rendered in italic style.
        /// </summary>
        public bool Italic { get; init; }

        /// <summary>
        /// The text alignment.
        /// </summary>
        public TextAlignment Alignment { get; init; }

        /// <summary>
        /// Flags to specify whether to measure bounds and/or render the text. 
        /// This allows for performance optimizations when only measurements are needed without rendering, or vice versa.
        /// </summary>
        public required TextRenderFlags RenderFlags { get; init; }

        public TextCreationParameters()
        {
        }
    }

    [Flags]
    public enum TextRenderFlags
    {
        MeasureBounds = 1 << 0,
        MeasureUnrestrictedBounds = 1 << 1,
        MeasureAll = MeasureBounds | MeasureUnrestrictedBounds,
        Render = 1 << 2,
    }

    public readonly struct TextCreationResult
    {
        /// <summary>
        /// The resulting texture of the rendered text.
        /// </summary>
        public Texture? Texture { get; init; }

        /// <summary>
        /// The bounds that were requested to restrict the text within. This is the same as the <see cref="TextCreationParameters.RestrictBounds"/> provided when creating the text.
        /// </summary>
        public Vector2 RequestedRestrictBounds { get; init; }

        /// <summary>
        /// The size of the rendered text as requested by the parameters. This may be larger than <see cref="DrawSize"/> if the text was restricted by the provided bounds, or smaller than <see cref="UnrestrictedBoundsSize"/> if the text contains empty space that was cropped out.
        /// </summary>
        public Vector2 BoundsSize { get; init; }

        /// <summary>
        /// The size of the rendered text without any restrictions applied. This may be larger than <see cref="BoundsSize"/> if the text was restricted by the provided bounds.
        /// You must pass <see cref="TextRenderFlags.MeasureUnrestrictedBounds"/> when creating the text to populate this value, otherwise it will be default(Vector2.Zero).
        /// </summary>
        public Vector2 UnrestrictedBoundsSize { get; init; }

        /// <summary>
        /// The actual size of the rendered text texture. This may be smaller than <see cref="BoundsSize"/> if the text was restricted by the provided bounds, or smaller than <see cref="UnrestrictedBoundsSize"/> if the text contains empty space that was cropped out.
        /// It may also be larger than both <see cref="BoundsSize"/> and <see cref="UnrestrictedBoundsSize"/> if the text was restricted by the provided bounds but still contains sub-pixel glyphs that require a larger texture to render.
        /// </summary>
        public Vector2 DrawSize => Texture is null ? Vector2.Zero : Texture.Size;

        /// <summary>
        /// Whether the text was restricted by the provided bounds. This is true if either the width or height of the actual rendered text is smaller than the requested restriction bounds.
        /// </summary>
        public bool IsRestrictedRequested => RequestedRestrictBounds.X > 0 || RequestedRestrictBounds.Y > 0;

        /// <summary>
        /// Whether the text was actually restricted. 
        /// This is true if the text was requested to be restricted and either the width or height of the actual rendered text is smaller than the requested restriction bounds.
        ///  Note that even if the text was requested to be restricted, it may not be actually restricted if the text fits within the restriction bounds, and in that case <see cref="DrawSize"/> may be equal to <see cref="BoundsSize"/> instead of <see cref="UnrestrictedBoundsSize"/>.
        /// </summary>
        public bool IsRestricted => IsRestrictedRequested &&
            (DrawSize.X < UnrestrictedBoundsSize.X || DrawSize.Y < UnrestrictedBoundsSize.Y);
    }
}
