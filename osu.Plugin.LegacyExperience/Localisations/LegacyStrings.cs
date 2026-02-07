using osu.Framework.Localisation;

namespace osu.Plugin.LegacyExperience.Localisations;

public static class LegacyStrings
{
    #region Player

    public static LocalisableString Player_ToggleScoreboard(object key) => new TranslatableString(getKey(nameof(Player_ToggleScoreboard)), "Hit <{0}> to toggle scoreboard!", key);

    public static LocalisableString Player_ScoreBoardShowStatus => new TranslatableString(getKey(nameof(Player_ScoreBoardShowStatus)), "The scoreboard will be hidden after this break ends!");

    public static LocalisableString Player_ScoreBoardShowStatus2 => new TranslatableString(getKey(nameof(Player_ScoreBoardShowStatus2)), "The scoreboard will be shown at all times!");

    #endregion // Player

    #region ModSelection

    public static LocalisableString ModSelection_Title => new TranslatableString(getKey(nameof(ModSelection_Title)), "Mods provide different ways to enjoy gameplay. Some have an effect on the score you can achieve during ranked play. Others are just for fun.");

    public static LocalisableString ModSelection_Reduction => new TranslatableString(getKey(nameof(ModSelection_Reduction)), "Difficulty Reduction");

    public static LocalisableString ModSelection_Increase => new TranslatableString(getKey(nameof(ModSelection_Increase)), "Difficulty Increase");

    public static LocalisableString ModSelection_Special => new TranslatableString(getKey(nameof(ModSelection_Special)), "Special");

    public static LocalisableString ModSelection_Reset => new TranslatableString(getKey(nameof(ModSelection_Reset)), "Reset All Mods");

    #endregion // ModSelection

    #region ModSelection_Mod

    public static LocalisableString ModSelection_Mod_Easy => new TranslatableString(getKey(nameof(ModSelection_Mod_Easy)), "Reduces overall difficulty - larger circles, more forgiving HP drain, less accuracy required.");

    public static LocalisableString ModSelection_Mod_NoFail => new TranslatableString(getKey(nameof(ModSelection_Mod_NoFail)), "You can't fail. No matter what.");

    public static LocalisableString ModSelection_Mod_HalfTime => new TranslatableString(getKey(nameof(ModSelection_Mod_HalfTime)), "Less zoom.");

    public static LocalisableString ModSelection_Mod_HardRock => new TranslatableString(getKey(nameof(ModSelection_Mod_HardRock)), "Everything just got a bit harder...");

    public static LocalisableString ModSelection_Mod_SuddenDeath => new TranslatableString(getKey(nameof(ModSelection_Mod_SuddenDeath)), "Miss a note and fail.");

    public static LocalisableString ModSelection_Mod_Perfect => new TranslatableString(getKey(nameof(ModSelection_Mod_Perfect)), "SS or quit.");

    public static LocalisableString ModSelection_Mod_DoubleTime => new TranslatableString(getKey(nameof(ModSelection_Mod_DoubleTime)), "Zoooooooooom.");

    public static LocalisableString ModSelection_Mod_Nightcore => new TranslatableString(getKey(nameof(ModSelection_Mod_Nightcore)), "uguuuuuuuu");

    public static LocalisableString ModSelection_Mod_Hidden => new TranslatableString(getKey(nameof(ModSelection_Mod_Hidden)), "Play with no approach circles and fading notes for a slight score advantage.");

    public static LocalisableString ModSelection_Mod_Flashlight => new TranslatableString(getKey(nameof(ModSelection_Mod_Flashlight)), "Restricted view area.");

    public static LocalisableString ModSelection_Mod_Relax => new TranslatableString(getKey(nameof(ModSelection_Mod_Relax)), "You don't need to click.\nGive your clicking/tapping fingers a break from the heat of things.\n** UNRANKED **");

    public static LocalisableString ModSelection_Mod_Relax2 => new TranslatableString(getKey(nameof(ModSelection_Mod_Relax2)), "Automatic cursor movement - just follow the rhythm.\n** UNRANKED **");

    public static LocalisableString ModSelection_Mod_Relax_CatchTheBeat => new TranslatableString(getKey(nameof(ModSelection_Mod_Relax_CatchTheBeat)), "Use the mouse to control the catcher.\n** UNRANKED **");

    public static LocalisableString ModSelection_Mod_Relax_Taiko => new TranslatableString(getKey(nameof(ModSelection_Mod_Relax_Taiko)), "Relax! You will no longer get dizzyfied by ninja-like spinners, demanding drumrolls or unexpected katu's.\n** UNRANKED **");

    public static LocalisableString ModSelection_Mod_SpunOut => new TranslatableString(getKey(nameof(ModSelection_Mod_SpunOut)), "Spinners will be automatically completed.");

    public static LocalisableString ModSelection_Mod_Autoplay => new TranslatableString(getKey(nameof(ModSelection_Mod_Autoplay)), "Watch a perfect automated play through the song.");

    public static LocalisableString ModSelection_Mod_Easy_OsuMania => new TranslatableString(getKey(nameof(ModSelection_Mod_Easy_OsuMania)), "Reduces overall difficulty - more forgiving HP drain, less accuracy required.");

    public static LocalisableString ModSelection_Mod_Hidden_OsuMania => new TranslatableString(getKey(nameof(ModSelection_Mod_Hidden_OsuMania)), "The notes fade out before you hit them!");

    public static LocalisableString ModSelection_Mod_Fade_OsuMania => new TranslatableString(getKey(nameof(ModSelection_Mod_Fade_OsuMania)), "The notes appear lower from the top!");

    public static LocalisableString ModSelection_Mod_Random_OsuMania => new TranslatableString(getKey(nameof(ModSelection_Mod_Random_OsuMania)), "Shuffle around the notes!");

    public static LocalisableString ModSelection_Mod_KeyCoop_OsuMania => new TranslatableString(getKey(nameof(ModSelection_Mod_KeyCoop_OsuMania)), "Double the key amount, double the fun!");

    public static LocalisableString ModSelection_Mod_Hidden_Taiko => new TranslatableString(getKey(nameof(ModSelection_Mod_Hidden_Taiko)), "The notes fade out before you hit them!");

    public static LocalisableString ModSelection_Mod_Easy_Taiko => new TranslatableString(getKey(nameof(ModSelection_Mod_Easy_Taiko)), "Reduces overall difficulty - notes move slower, less accuracy required.");

    // This string exists in osu!common.OsuString, but is not localised in osu!stable. We still provide a translation for it in the future.
    public static LocalisableString ModSelection_Mod_ScoreV2 => new TranslatableString(getKey(nameof(ModSelection_Mod_ScoreV2)), "Try the future scoring system!\n** UNRANKED **");

    #endregion // ModSelection_Mod

    private static string getKey(string key) => LegacyLocalisationManager.GetKey(key);
}
