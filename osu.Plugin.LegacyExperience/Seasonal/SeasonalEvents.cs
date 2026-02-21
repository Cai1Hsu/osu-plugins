namespace osu.Plugin.LegacyExperience.Seasonal;

/// <summary>
/// The seasonal events that may be active in the game, used for determining which seasonal UI to show.
/// Using [Flags] just to allow better flexibility in activation and deactivation of events.
/// </summary>
[Flags]
public enum SeasonalEvents
{
    None = 0,
    Halloween = 1 << 0,
    Christmas = 1 << 1,
    Summer = 1 << 2
}
