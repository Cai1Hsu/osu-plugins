namespace osu.Plugin.LegacyExperience.Mods;

/// <summary>
/// The type of a <see cref="LegacyMod"/>. This is used to categorize legacy mods in the mod selection screen.
/// </summary>
public enum LegacyModType
{
    /// <summary>
    /// Mods that reduce difficulty and score multiplier, such as Easy and NoFail.
    /// </summary>
    Reduction,
    /// <summary>
    /// Mods that increase difficulty and score multiplier, such as HardRock and DoubleTime.
    /// </summary>
    Increase,
    /// <summary>
    /// Mods that do not fit into the above categories, such as Autoplay and Cinema. Some mods may be unrankable.
    /// </summary>
    Special,
}
