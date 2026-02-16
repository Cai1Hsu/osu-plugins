using osuTK;
using osu.Framework.Graphics.Textures;

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// Contains type definitions for stable's font rendering system (NativeText).
/// Use <see cref="ImageSharpNativeText"/> or <see cref="GdipNativeText"/> for actual implementations.
/// </summary>
public static partial class NativeText
{
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
        /// </summary>
        public bool IsRestricted => IsRestrictedRequested &&
            UnrestrictedBoundsSize != Vector2.Zero &&
            (DrawSize.X < UnrestrictedBoundsSize.X || DrawSize.Y < UnrestrictedBoundsSize.Y);
    }
}
