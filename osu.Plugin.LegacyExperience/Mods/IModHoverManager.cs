namespace osu.Plugin.LegacyExperience.Mods;

public interface IModHoverManager
{
    /// <summary>
    /// Requests the hover sample to be played. This is used to debounce hover samples when hovering over multiple mod icons in quick succession.
    /// </summary>
    /// <returns>>Whether the hover sample should be played.</returns>
    bool RequestHoverSample();
}
