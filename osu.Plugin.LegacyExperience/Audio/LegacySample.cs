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
}
