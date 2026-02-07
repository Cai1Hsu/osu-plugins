using System.Diagnostics.CodeAnalysis;

namespace osu.Plugin.LegacyExperience.Mods;

[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "We want the enum values to match the stable mod names for easier mapping.")]
public enum LegacyMod
{
    autoplay,
    cinema,
    doubletime,
    easy,
    fadein,
    fadeout,
    flashlight,
    halftime,
    hardrock,
    hidden,
    key1,
    key2,
    key3,
    key4,
    key5,
    key6,
    key7,
    key8,
    key9,
    keycoop,
    mirror, // ManiaModMirror, do not get confused with OsuModMirror.
    nightcore,
    nofail,
    perfect,
    random, // ManiaModRandom, do not get confused with OsuModRandom.
    relax,
    relax2, // autopilot
    scorev2,
    spunout,
    suddendeath,
    target,
}
