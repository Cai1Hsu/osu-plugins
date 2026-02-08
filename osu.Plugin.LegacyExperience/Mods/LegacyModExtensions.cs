using System.Diagnostics.CodeAnalysis;
using osu.Game.Rulesets.Mods;

namespace osu.Plugin.LegacyExperience.Mods;

public static class LegacyModExtensions
{
    /// <summary>
    /// Tries to get the corresponding <see cref="LegacyMod"/> for a given <see cref="Mod"/>. This is used to map the mods used in stable replays to their corresponding legacy mods.
    /// </summary>
    /// <remarks>
    /// This is a LOSSY mapping, you will need ruleset context to get lazer mod from legacy mod.
    /// </remarks>
    /// <param name="mod">The lazer mod to be mapped to a legacy mod.</param>
    /// <param name="legacyMod">The corresponding legacy mod, or null if the given mod does not have a corresponding legacy mod.</param>
    /// <returns>Whether a corresponding legacy mod was found for the given mod.</returns>
    public static bool TryGetLegacyMod(this Mod mod, [NotNullWhen(true)] out LegacyMod? legacyMod)
    {
        legacyMod = mod switch
        {
            // match ruleset-specific legacy mods first since they may derive from other mods, for example, mania's fadein mod derives from hidden mod.
            _ when mapRulesetSpecificLegacyMod(mod) is LegacyMod rulesetSpecific => rulesetSpecific,
            ModCinema => LegacyMod.Cinema, // cinema derives from autoplay
            ModAutoplay => LegacyMod.Autoplay,
            ModNightcore => LegacyMod.Nightcore, // nightcore derives from doubletime
            ModDoubleTime => LegacyMod.DoubleTime,
            ModEasy => LegacyMod.Easy,
            ModFlashlight => LegacyMod.Flashlight,
            ModHalfTime => LegacyMod.HalfTime,
            ModHardRock => LegacyMod.HardRock,
            ModHidden => LegacyMod.Hidden,
            ModNoFail => LegacyMod.NoFail,
            ModPerfect => LegacyMod.Perfect,
            ModRelax => LegacyMod.Relax,
            ModSuddenDeath => LegacyMod.SuddenDeath,
            _ => null,
        };

        return legacyMod is not null;
    }

    private static LegacyMod? mapRulesetSpecificLegacyMod(Mod mod)
    {
        // reflection here to avoid taking a hard dependency on the ruleset assemblies
        var modType = mod.GetType();

        return mod switch
        {
            _ when modType == ManiaModFadeIn => LegacyMod.FadeIn,
            _ when modType == ManiaModDualStages => LegacyMod.KeyCoop,
            _ when modType == ManiaModKeys(1) => LegacyMod.Key1,
            _ when modType == ManiaModKeys(2) => LegacyMod.Key2,
            _ when modType == ManiaModKeys(3) => LegacyMod.Key3,
            _ when modType == ManiaModKeys(4) => LegacyMod.Key4,
            _ when modType == ManiaModKeys(5) => LegacyMod.Key5,
            _ when modType == ManiaModKeys(6) => LegacyMod.Key6,
            _ when modType == ManiaModKeys(7) => LegacyMod.Key7,
            _ when modType == ManiaModKeys(8) => LegacyMod.Key8,
            _ when modType == ManiaModKeys(9) => LegacyMod.Key9, // key10 is not supported in stable
            _ when modType == ManiaModMirror => LegacyMod.Mirror, // do not get confused with OsuModMirror
            _ when modType == ManiaModRandom => LegacyMod.Random, // do not get confused with OsuModRandom
            _ when modType == OsuModAutopilot => LegacyMod.Relax2,
            _ when modType == OsuModSpunOut => LegacyMod.SpunOut,
            _ when modType == OsuModTarget => LegacyMod.Target,
            // key10 is not supported in stable
            // fade out is no longer used in both stable and lazer, so we don't support it either.
            // key coop is not yet supported in lazer
            _ => null,
        };
    }

    public static readonly Type? ManiaModFadeIn = Type.GetType("osu.Game.Rulesets.Mania.Mods.ManiaModFadeIn, osu.Game.Rulesets.Mania");

    public static Type? ManiaModKeys(int keyCount) => keyCount < 1 || keyCount > 9 ? null : modKeys[keyCount - 1];

    public static readonly Type? ManiaModMirror = Type.GetType("osu.Game.Rulesets.Mania.Mods.ManiaModMirror, osu.Game.Rulesets.Mania");

    public static readonly Type? ManiaModRandom = Type.GetType("osu.Game.Rulesets.Mania.Mods.ManiaModRandom, osu.Game.Rulesets.Mania");

    public static readonly Type? ManiaModDualStages = Type.GetType("osu.Game.Rulesets.Mania.Mods.ManiaModDualStages, osu.Game.Rulesets.Mania");

    public static readonly Type? OsuModAutopilot = Type.GetType("osu.Game.Rulesets.Osu.Mods.OsuModAutopilot, osu.Game.Rulesets.Osu");

    public static readonly Type? OsuModSpunOut = Type.GetType("osu.Game.Rulesets.Osu.Mods.OsuModSpunOut, osu.Game.Rulesets.Osu");

    public static readonly Type? OsuModTarget = Type.GetType("osu.Game.Rulesets.Osu.Mods.OsuModTargetPractice, osu.Game.Rulesets.Osu");

    private static readonly Type?[] modKeys = new Type[9];

    static LegacyModExtensions()
    {
        for (int i = 0; i < 9; i++)
        {
            var type = Type.GetType($"osu.Game.Rulesets.Mania.Mods.ManiaModKey{i + 1}, osu.Game.Rulesets.Mania");
            modKeys[i] = type;
        }
    }
}
