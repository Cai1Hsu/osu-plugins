using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Plugin.LegacyExperience.Seasonal;

namespace osu.Plugin.LegacyExperience.Tests.Seasonal;

public class TestSeasonalConfig : ISeasonalConfig
{
    public Bindable<SeasonalEvents> ActiveEvents = new();

    public TestSeasonalConfig()
    {
        ActiveEvents.BindValueChanged(e => SeasonalUIConfig.UpdateEventConfig(this, e.NewValue), true);
    }

    public string? LogoTexture { get; init; }

    public string? LogoHeartbeat { get; init; }

    public Colour4 SnowColour { get; init; }

    public string? SnowflakeTexture { get; init; }
}
