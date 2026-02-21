using osu.Framework.Graphics;

namespace osu.Plugin.LegacyExperience.Seasonal;

public interface ISeasonalConfig
{
    string? LogoHeartbeat => null;

    Colour4 SnowColour => Colour4.White;

    string? SnowflakeTexture => null;
}
