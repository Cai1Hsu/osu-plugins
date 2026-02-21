using System.Reflection;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using LazerSeasonalUIConfig = osu.Game.Seasonal.SeasonalUIConfig;

namespace osu.Plugin.LegacyExperience.Seasonal;

public class SeasonalUIConfig : ISeasonalConfig
{
    public static SeasonalEvents ActiveEvents
    {
        get
        {
            var events = activateEvents;

            // currently we still relies on manual activation of events, 
            // as we don't have a way to determine which events are active.
            // Both stable and lazer depends on update with manual activation,
            // but lazer only has Christmas event, so we can just activate it by default if no events are activated and seasonal UI is enabled.
            if (LazerSeasonalUIConfig.ENABLED)
                events |= SeasonalEvents.Christmas;

            var dateTime = DateTimeOffset.UtcNow;

            // Halloween events are active from 22nd October to 2st November in update these years,
            if (dateTime.Month is 10 && dateTime.Day >= 22
                || dateTime.Month is 11 && dateTime.Day <= 2)
                events |= SeasonalEvents.Halloween;

            // A rough estimation of summer event period, may be use a web reqest to determine?
            if (dateTime.Month is >= 6 && dateTime.Month <= 8)
                events |= SeasonalEvents.Summer;

            return events & deactiveEventMask;
        }
    }

    private static readonly SeasonalEvents activateEvents = SeasonalEvents.None;

    // in case we want to force deactivation of some events without touching the date check,
    // we can set the corresponding bits in this mask to 0.
    private static readonly SeasonalEvents deactiveEventMask = (SeasonalEvents)~0;

    string? ISeasonalConfig.LogoTexture { get; init; }

    string? ISeasonalConfig.LogoHeartbeat { get; init; }

    Colour4 ISeasonalConfig.SnowColour { get; init; }

    string? ISeasonalConfig.SnowflakeTexture { get; init; }

    public SeasonalUIConfig()
    {
        UpdateEventConfig(this, ActiveEvents);
    }

    internal static void UpdateEventConfig(ISeasonalConfig config, SeasonalEvents events)
    {
        // we do want modification to these properties generally,
        // but for testing purposes we need to be able to update them on the fly, 
        // so we will use reflection to bypass the init-only restriction.

        if (events.HasFlagFast(SeasonalEvents.Christmas))
        {
            // lazer actually packed the christmas's heartbeat sample, so we can use it directly.
            setProp(nameof(ISeasonalConfig.LogoHeartbeat), "Menu/osu-logo-heartbeat-bell");
            setProp(nameof(ISeasonalConfig.LogoTexture), "Seasonal/Christmas/menu-osu");
        }

        if (events.HasFlagFast(SeasonalEvents.Halloween))
            setProp(nameof(ISeasonalConfig.SnowColour), new Colour4(255, 201, 14, 255));

        if (events.HasFlagFast(SeasonalEvents.Summer))
            setProp(nameof(ISeasonalConfig.SnowflakeTexture), "Seasonal/Summer/menu-beachball");
        else if (events.HasFlagFast(SeasonalEvents.Halloween))
            setProp(nameof(ISeasonalConfig.SnowflakeTexture), "Seasonal/Halloween/menu-snow");

        void setProp(string prop, object? value)
        {
            var setter = config.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                .SetMethod;

            if (setter is null)
                throw new InvalidOperationException($"Property {prop} not found in {config.GetType()}.");

            setter.Invoke(config, new[] { value });
        }
    }
}
