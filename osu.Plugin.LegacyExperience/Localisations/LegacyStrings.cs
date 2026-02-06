using osu.Framework.Localisation;

namespace osu.Plugin.LegacyExperience.Localisations;

public static class LegacyStrings
{
    #region Player

    public static LocalisableString Player_ToggleScoreboard(object key) => new TranslatableString(getKey(nameof(Player_ToggleScoreboard)), "Hit <{0}> to toggle scoreboard!", key);

    public static LocalisableString Player_ScoreBoardShowStatus => new TranslatableString(getKey(nameof(Player_ScoreBoardShowStatus)), "The scoreboard will be hidden after this break ends!");

    public static LocalisableString Player_ScoreBoardShowStatus2 => new TranslatableString(getKey(nameof(Player_ScoreBoardShowStatus2)), "The scoreboard will be shown at all times!");

    #endregion // Player

    private static string getKey(string key) => LegacyLocalisationManager.GetKey(key);
}
