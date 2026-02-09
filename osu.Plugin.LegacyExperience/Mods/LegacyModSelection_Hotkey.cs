using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osuTK.Input;

namespace osu.Plugin.LegacyExperience.Mods;

partial class LegacyModSelection
{
    /// <summary>
    /// A hotkey combination associated with certain legacy mods.
    /// </summary>
    /// <param name="key">The main key of the hotkey combination.</param>
    /// <param name="associatedMods">The mods associated with this hotkey combination.</param>
    /// <param name="useCycle">Whether the hotkey combination should cycle through associated mods when there are multiple, or use accurate selection to directly select a specific mod. Accurate selection will also allow deselecting the currently selected mod by pressing the hotkey combination again, while cycle will not.</param>
    /// <param name="shiftPressed">Whether the hotkey combination requires the shift key to be pressed. If true, the hotkey will only be triggered when the shift key is pressed. If false, the state of the shift key will be ignored and the hotkey will be triggered regardless of whether the shift key is pressed.</param>
    private readonly record struct CombinationHotKey(
        Key key,
        LegacyMod[] associatedMods,
        bool useCycle = true,
        bool shiftPressed = false);

    private static readonly CombinationHotKey[] hotkeys =
    [
        new (Key.Q, new[] { LegacyMod.Easy }),
        new (Key.W, new[] { LegacyMod.NoFail }),
        new (Key.E, new[] { LegacyMod.HalfTime }),

        new (Key.A, new[] { LegacyMod.HardRock }),
        new (Key.S, combination_SDPF),
        new (Key.S, new[] { LegacyMod.Perfect }, shiftPressed: true, useCycle: false),
        new (Key.D, combination_DTNC),
        new (Key.D, new[] { LegacyMod.Nightcore }, shiftPressed: true, useCycle: false),
        new (Key.F, combination_FIHD),
        new (Key.F, new[] { LegacyMod.FadeIn }, shiftPressed: true, useCycle: false),
        new (Key.G, new[] { LegacyMod.Flashlight }),

        new (Key.Z, new[] { LegacyMod.Relax }),
        new (Key.Z, combination_KEYN),
        new (Key.X, new[] { LegacyMod.Relax2 }),
        new (Key.X, new[] { LegacyMod.Random }, shiftPressed: true, useCycle: false),
        new (Key.C, new[] { LegacyMod.SpunOut }),
        new (Key.V, combination_ATCN),
        new (Key.V, new[] { LegacyMod.Cinema }, shiftPressed: true, useCycle: false),
        new (Key.B, new[] { LegacyMod.ScoreV2 }),

        // Key.Number1 and Key.Number2 are not useable due to conflict with dialog's option hotkeys,
        // however, this matches stable's behavior so we keep it for consistency.
        ..Enumerable.Range(0, 9)
                    .Select(i => new CombinationHotKey(Key.Number1 + i, new[] { LegacyMod.Key1 + i }, useCycle: false))
    ];

    private (UserModSwitch, CombinationHotKey)? findTargetModSwitch(Key key, bool shiftPressed, Func<LegacyMod[], UserModSwitch?> getModSwitch)
    {
        (UserModSwitch, CombinationHotKey)? target = null;

        foreach (var hotkey in hotkeys)
        {
            // nested if looks bad, but semantically clearer than combining conditions with &&
            if (hotkey.key == key)
            {
                // shiftPressed is a required condition, but if the hotkey doesn't require shift, 
                // we should ignore the state of shift key and allow triggering the hotkey regardless of whether shift is pressed.
                if (target is null || hotkey.shiftPressed == shiftPressed)
                {
                    if (getModSwitch(hotkey.associatedMods) is UserModSwitch modSwitch)
                    {
                        target = (modSwitch, hotkey);
                    }
                }
            }
        }

        return target;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (base.OnKeyDown(e))
            return true;

        if (e.Repeat)
            return false;

        bool shiftPressed = e.ShiftPressed;

        var target = findTargetModSwitch(e.Key, shiftPressed, associatedMods => Content.ChildrenOfType<UserModSwitch>()
                                                                                       .FirstOrDefault(m => associatedMods.Intersect(m.Mods).Any()));

        if (target is null)
            return false;

        var (modSwitch, hotkey) = target.Value;

        if (hotkey.useCycle)
        {
            modSwitch.Cycle();
        }
        else
        {
            var targetMod = hotkey.associatedMods.Single();
            var currentMod = modSwitch.CurrentInfo.SelectedMod;

            if (currentMod is null || currentMod != targetMod)
                // WTF? No Linq on IReadOnlyList? Why?
                modSwitch.SelectMod(modSwitch.Mods.Select(static (m, i) => (m, i)).First(t => t.m == targetMod).i);
            else
                modSwitch.ClearSelection();
        }

        return true;
    }
}
