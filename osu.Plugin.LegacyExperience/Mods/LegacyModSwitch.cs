using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Rulesets;
using osu.Plugin.LegacyExperience.Localisations;
using osuTK.Input;

namespace osu.Plugin.LegacyExperience.Mods;

public partial class LegacyModSwitch : CompositeDrawable
{
    private LegacyMod[] mods;

    public LegacyModSwitch(LegacyMod[] mods)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mods.Length, 1, nameof(mods));

        this.mods = mods;
        this.currentSelection = mods.Length; // start with no mod selected
    }

    private int currentSelection;
    private ClickableModDisplay[] modDisplays = null!;

    private InputManager inputManager = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        modDisplays = new ClickableModDisplay[mods.Length];

        for (int i = 0; i < mods.Length; i++)
        {
            var mod = mods[i];

            var display = new ClickableModDisplay(mod)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Action = onModClick,
            };

            if (i != DisplayIndex)
                display.Alpha = 0;

            modDisplays[i] = display;
            AddInternal(display);
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        inputManager = GetContainingInputManager();
    }

    /// <summary>
    /// Represent the current selection of the mod switch. The value is the index of the selected mod in the mods array, or mods.Length if no mod is selected.
    /// </summary>
    public int CurrentSelection => currentSelection;

    /// <summary>
    /// Represent the index of the mod to be displayed. This is used for display purposes only, and is always in the range of [0, mods.Length - 1]. 
    /// When no mod is selected, the first mod (index 0) will be displayed.
    /// </summary>
    public int DisplayIndex => currentSelection % mods.Length;

    /// <summary>
    /// Represent the total number of selections, which is the number of mods plus one for the no mod selection.
    /// </summary>
    public int TotalSelections => mods.Length + 1;

    /// <summary>
    /// The currently activated mod. This will be null when no mod is selected (CurrentSelection == mods.Length).
    /// </summary>
    public LegacyMod? SelectedMod => currentSelection < mods.Length ? mods[currentSelection] : null;

    private void onModClick()
    {
        var current = modDisplays.ElementAtOrDefault(currentSelection);

        if (current is not null)
            deactivateMod(current);

        // stable uses contains instead of checking the activation button,
        // this means when you click with right button previously pressed, the direction will still be backwards.
        // We keep this behaviour in case any user relies on it, but it is not recommended to use right click for mod switching.
        int direction = inputManager.CurrentState.Mouse.IsPressed(MouseButton.Right) ? -1 : 1;

        currentSelection = (currentSelection + direction + TotalSelections) % TotalSelections;

        var next = modDisplays.ElementAtOrDefault(CurrentSelection);

        if (next is not null)
            activateMod(next);
        else
            resetMod(modDisplays[DisplayIndex]);
    }

    private void resetMod(ClickableModDisplay mod)
    {
        mod.FadeIn(100)
           .ScaleTo(1f, 400, Easing.OutElastic)
           .RotateTo(0f, 400, Easing.OutElastic);
    }

    private void activateMod(ClickableModDisplay mod)
    {
        mod.FadeIn(100)
           .ScaleTo(1.2f, 400, Easing.OutElastic)
           .RotateTo(activate_rotation, 400, Easing.OutElastic);
    }

    private const float activate_rotation = (float)(0.1 * 180 / Math.PI);

    private void deactivateMod(ClickableModDisplay mod)
    {
        mod.FadeOut(100)
           .ScaleTo(1f, 400, Easing.OutElastic)
           .RotateTo(0f, 400, Easing.OutElastic);
    }

    private partial class ClickableModDisplay : ClickableContainer, IHasLegacyTooltip
    {
        private readonly LegacyModDisplay modDisplay;

        public ClickableModDisplay(LegacyMod mod)
        {
            Child = modDisplay = new LegacyModDisplay(mod);

            AutoSizeAxes = Axes.Both;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Right)
            {
                TriggerClick();
                return true;
            }

            return base.OnMouseDown(e);
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<Ruleset>? ruleset)
        {
            var playMode = ruleset?.Value.RulesetInfo.OnlineID ?? 0;

            tooltipText = modDisplay.Mod switch
            {
                LegacyMod.Easy when playMode == 1 => LegacyStrings.ModSelection_Mod_Easy_Taiko,
                LegacyMod.Easy when playMode == 3 => LegacyStrings.ModSelection_Mod_Easy_OsuMania,
                LegacyMod.Easy => LegacyStrings.ModSelection_Mod_Easy,

                LegacyMod.NoFail => LegacyStrings.ModSelection_Mod_NoFail,
                LegacyMod.HalfTime => LegacyStrings.ModSelection_Mod_HalfTime,
                LegacyMod.HardRock => LegacyStrings.ModSelection_Mod_HardRock,
                LegacyMod.SuddenDeath => LegacyStrings.ModSelection_Mod_SuddenDeath,
                LegacyMod.Perfect => LegacyStrings.ModSelection_Mod_Perfect,
                LegacyMod.DoubleTime => LegacyStrings.ModSelection_Mod_DoubleTime,
                LegacyMod.Nightcore => LegacyStrings.ModSelection_Mod_Nightcore,

                LegacyMod.Hidden when playMode == 1 => LegacyStrings.ModSelection_Mod_Hidden_Taiko,
                LegacyMod.Hidden when playMode == 3 => LegacyStrings.ModSelection_Mod_Hidden_OsuMania,
                LegacyMod.Hidden => LegacyStrings.ModSelection_Mod_Hidden,

                LegacyMod.Flashlight => LegacyStrings.ModSelection_Mod_Flashlight,

                LegacyMod.KeyCoop => LegacyStrings.ModSelection_Mod_KeyCoop_OsuMania,
                LegacyMod.Random => LegacyStrings.ModSelection_Mod_Random_OsuMania,

                LegacyMod.Relax when playMode == 1 => LegacyStrings.ModSelection_Mod_Relax_Taiko,
                LegacyMod.Relax when playMode == 2 => LegacyStrings.ModSelection_Mod_Relax_CatchTheBeat,
                LegacyMod.Relax => LegacyStrings.ModSelection_Mod_Relax,

                LegacyMod.Relax2 => LegacyStrings.ModSelection_Mod_Relax2,
                LegacyMod.SpunOut => LegacyStrings.ModSelection_Mod_SpunOut,
                LegacyMod.Autoplay => LegacyStrings.ModSelection_Mod_Autoplay,
                LegacyMod.ScoreV2 => LegacyStrings.ModSelection_Mod_ScoreV2,
                _ => string.Empty,
            };
        }

        private LocalisableString tooltipText;
        LocalisableString IHasLegacyTooltip.TooltipText => tooltipText;
    }
}
