using System.Reflection;
using System.Resources;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
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

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// Abstract base class providing common functionality for rendering text using stable's font rendering system.
/// This class handles font loading, caching, and script detection.
/// </summary>
public abstract partial class NativeTextBase : Component, INativeText
{
    protected Bindable<string> frameworkLocale = null!;

    [Resolved]
    private IRenderer renderer { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load(FrameworkConfigManager frameworkConfig)
    {
        frameworkLocale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);

        loadOsuUI();
        populateFontCollection();

        initializeTextureAtlas();
    }

    private TextureAtlas? textureAtlas;

    // of's TextureStore limits texture size to 1024 due to mipmapping's performance impact,
    // but we are not mipmapping for text textures, so we can use larger textures to reduce atlas usage and improve performance.
    private const int max_atlas_size = 4096;

    private void initializeTextureAtlas()
    {
        int atlasSize = Math.Min(renderer.MaxTextureSize, max_atlas_size);
        textureAtlas = new TextureAtlas(renderer, atlasSize, atlasSize, manualMipmaps: true);
    }

    /// <summary>
    /// Get a texture for the given dimensions. Falls back to creating a new texture if atlas allocation fails.
    /// </summary>
    protected Texture CreateTexture(int width, int height)
    {
        return (textureAtlas?.Add(width, height) ?? renderer.CreateTexture(width, height))!;
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
            // so let's try the default installation path (~\AppData\Local\osu!) for now.
            new NativeStorage(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!")),
            lazerStorage,
            // also attempt the directory of our assembly as a last resort.
            new NativeStorage(Path.GetDirectoryName(typeof(NativeTextBase).Assembly.Location) ?? string.Empty),
        };

        foreach (var storage in fallbackStorages)
        {
            if (storage is null)
                continue;

            osu_ui_Assembly = TryLoadOsuUIAssembly(storage);

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

        loadOsuUI();
        populateFontCollection();
    }

    private const string osu_ui_dll = "osu!ui.dll";

    protected Assembly? TryLoadOsuUIAssembly(Storage possibleStorage)
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
        {
            var enumerator = reader.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string resourceName = (string)enumerator.Key;

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

                using var fontStream = new MemoryStream(resourceData, sizeof(int), length, false, publiclyVisible: true);

                Logger.Log($"Loaded font {resourceName} from {osu_ui_dll}", LoggingTarget.Runtime, LogLevel.Verbose);

                AddFont(resourceName, fontStream);
            }
        }
    }

    protected abstract void AddFont(string resourceName, MemoryStream fontStream);

    protected void ProcessFontSpecificGlyphAdjustments(ref ReadOnlyMemory<char> text, string fontName)
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

    protected string ResolveFontName(NativeText.LegacyFontFace fontFace, ReadOnlySpan<char> text, float size, bool bold)
    {
        string fontName = GetFontFace(fontFace);

        // stable's behaviour:
        if (fontFace is NativeText.LegacyFontFace.DefaultRegular or NativeText.LegacyFontFace.DefaultLight)
        {
            string? languageFont = GetLanguageSpecificFont(text);

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
    /// Detects CJK or special script characters and returns an appropriate system font name.
    /// Ported from stable's getLanguageSpecificFont.
    /// </summary>
    protected string? GetLanguageSpecificFont(ReadOnlySpan<char> text)
    {
        ScriptType script = DetectScript(text);

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
    protected static ScriptType DetectScript(ReadOnlySpan<char> text)
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
    /// Maps <see cref="NativeText.LegacyFontFace"/> enum to the actual font family name.
    /// </summary>
    internal static string GetFontFace(NativeText.LegacyFontFace fontFace)
    {
        return fontFace switch
        {
            NativeText.LegacyFontFace.DefaultRegular => "Aller",
            NativeText.LegacyFontFace.Tahoma => "Tahoma",
            NativeText.LegacyFontFace.FontAwesome => "FontAwesome",
            NativeText.LegacyFontFace.Exo2 => "Exo 2.0",
            NativeText.LegacyFontFace.Exo2Black => "Exo 2.0 Black",
            NativeText.LegacyFontFace.Exo2ExtraBold => "Exo 2.0 Extra Bold",
            NativeText.LegacyFontFace.Exo2ExtraLight => "Exo 2.0 Extra Light",
            NativeText.LegacyFontFace.Exo2Light => "Exo 2.0 Light",
            NativeText.LegacyFontFace.Exo2Medium => "Exo 2.0 Medium",
            NativeText.LegacyFontFace.Exo2SemiBold => "Exo 2.0 Semi Bold",
            NativeText.LegacyFontFace.Exo2Thin => "Exo 2.0 Thin",
            _ => "Aller Light",
        };
    }

    protected enum ScriptType
    {
        Latin,
        Japanese,
        Korean,
        Cyrillic,
        CjkUnified,
    }

    /// <summary>
    /// Implementations of <see cref="INativeText"/> must implement this method to create text textures.
    /// </summary>
    public abstract void CreateText(in NativeText.TextCreationParameters parameters, out NativeText.TextCreationResult result);
}
