namespace osu.Plugin.LegacyExperience.Graphics;

/// <summary>
/// Provides an interface for rendering text using stable's font rendering system (NativeText).
/// </summary>
public interface INativeText
{
    /// <summary>
    /// Creates a texture containing the rendered text based on the provided parameters.
    /// </summary>
    /// <param name="parameters">The text creation parameters.</param>
    /// <param name="result">The result of the text creation operation.</param>
    void CreateText(in NativeText.TextCreationParameters parameters, out NativeText.TextCreationResult result);
}
