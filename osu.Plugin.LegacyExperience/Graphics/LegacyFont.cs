using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// A static class that provides <see cref="FontUsage"/>s that best replicate the default font in stable.
/// </summary>
public static class LegacyFont
{
    /// <summary>
    /// The default font usage that best replicates the default font in stable(Aller).
    /// </summary>
    public static class Default
    {
        public static FontUsage Value => OsuFont.Inter;

        /// <summary>
        /// Gets a <see cref="FontUsage"/> with the default font family and specified size, weight, italics and fixed width.
        /// </summary>
        /// <param name="size">The size of the font. Note that the size is multiplied by the stable ratio to match the appearance in stable.</param>
        /// <param name="weight">The weight of the font.</param>
        /// <param name="italics">Whether the font is italicized.</param>
        /// <param name="fixedWidth">Whether the font is fixed width.</param>
        /// <returns>A <see cref="FontUsage"/> with the specified properties.</returns>
        public static FontUsage With(float size, FontWeight weight = FontWeight.Regular, bool italics = false, bool fixedWidth = false)
        {
            // im bored with multiplying the size by the stable ratio every time, so just do it here
            return Value.With(Typeface.Inter, size * LegacyExperiencePlugin.StableRatio, weight, italics, fixedWidth);
        }
    }
}
