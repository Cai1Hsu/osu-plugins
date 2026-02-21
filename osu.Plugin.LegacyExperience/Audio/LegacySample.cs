using System.ComponentModel;

namespace osu.Plugin.LegacyExperience.Audio;

/// <summary>
/// A list of legacy sample usages.
/// </summary>
public enum LegacySample
{
    [Description("select-difficulty")]
    select_difficulty,
    [Description("select-expand")]
    select_expand,
    [Description("check-off")]
    check_off,
    [Description("check-on")]
    check_on,
    [Description("click-short")]
    click_short,
    [Description("click-short-confirm")]
    click_short_confirm,
    [Description("menuclick")]
    menuclick,
    [Description("menuhit")]
    menuhit,
    [Description("sectionpass")]
    sectionpass,
    [Description("sectionfail")]
    sectionfail,
    [Description("heartbeat")]
    heartbeat,
    [Description("menuback")]
    menuback,
    [Description("menu-edit-click")]
    menu_edit_click,
    [Description("menu-freeplay-click")]
    menu_freeplay_click,
    [Description("menu-multiplayer-click")]
    menu_multiplayer_click,
    [Description("menu-play-click")]
    menu_play_click,
}
