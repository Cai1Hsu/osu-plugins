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
    /// <param name="shiftPressed">Whether the hotkey combination requires the shift key to be pressed. This also makes the hotkey use accurate selection instead of cycling when there are multiple associated mods.</param>
    private readonly record struct CombinationHotKey(
        Key key,
        LegacyMod[] associatedMods,
        bool shiftPressed = false);

    private static readonly CombinationHotKey[] hotkeys =
    {
        new (Key.Q, new[] { LegacyMod.Easy }),
        new (Key.W, new[] { LegacyMod.NoFail }),
        new (Key.E, new[] { LegacyMod.HalfTime }),

        new (Key.A, new[] { LegacyMod.HardRock }),
        new (Key.S, combination_SDPF),
        new (Key.S, new[] { LegacyMod.Perfect }, shiftPressed: true),
        new (Key.D, combination_DTNC),
        new (Key.D, new[] { LegacyMod.Nightcore }, shiftPressed: true),
        new (Key.F, combination_FIHD),
        new (Key.F, new[] { LegacyMod.FadeIn }, shiftPressed: true),
        new (Key.G, new[] { LegacyMod.Flashlight }),

        new (Key.Z, new[] { LegacyMod.Relax }),
        new (Key.Z, combination_KEYN),
        new (Key.X, new[] { LegacyMod.Relax2 }),
        new (Key.X, new[] { LegacyMod.Random }, shiftPressed: true),
        new (Key.C, new[] { LegacyMod.SpunOut }),
        new (Key.V, combination_ATCN),
        new (Key.V, new[] { LegacyMod.Cinema }, shiftPressed: true),
        new (Key.B, new[] { LegacyMod.ScoreV2 }),
    };

    private (UserModSwitch, LegacyMod[])? findTargetModSwitch(Key key, bool shiftPressed, Func<LegacyMod[], UserModSwitch?> getModSwitch)
    {
        (UserModSwitch, LegacyMod[])? target = null;

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
                        target = (modSwitch, hotkey.associatedMods);
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

        var (modSwitch, associatedMods) = target.Value;

        bool useCycle = !shiftPressed || associatedMods.Length > 1;

        if (useCycle)
        {
            modSwitch.Cycle();
        }
        else
        {
            var targetMod = associatedMods.Single();
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
