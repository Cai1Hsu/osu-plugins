using osu.Framework.Graphics;

namespace osu.Plugin.LegacyExperience.Seasonal;

public interface ISeasonalConfig
{
    string? LogoTexture { get; init; }
    string? LogoHeartbeat { get; init; }

    Colour4 SnowColour { get; init; }

    string? SnowflakeTexture { get; init; }
}
