using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Localisations;
using osuTK;
using osuTK.Input;

namespace osu.Plugin.LegacyExperience.Mods;

/// <summary>
/// Represents the selection state of a mod switch.
/// </summary>
public enum ModSelectionState
{
    /// <summary>
    /// A specific mod is selected.
    /// </summary>
    Selected,

    /// <summary>
    /// No mod is selected.
    /// </summary>
    NoSelection,

    /// <summary>
    /// The mod switch is disabled (darkened by 50%).
    /// Switching forward/backward will return to NoSelection state.
    /// </summary>
    Disabled,
}

/// <summary>
/// Contains information about the current selection state of a mod switch.
/// </summary>
public readonly record struct ModSelectionInfo(
    ModSelectionState State,
    int DisplayedIndex,
    int? SelectedIndex,
    LegacyMod? SelectedMod);

public partial class LegacyModSwitch : CompositeDrawable
{
    private readonly LegacyMod[] mods;

    public IReadOnlyList<LegacyMod> Mods => mods;

    public LegacyModSwitch(LegacyMod[] mods)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mods.Length, 1, nameof(mods));

        this.mods = mods;
        this.currentSelection = NoModSelection; // start with no mod selected

        // LegacyModSwitch can't be auto-sized because has to keep mod displays' position consistent,
        // so we set a fixed size that can fit all mod displays.
        // This is the grid size used in mod selection in stable(previously 85x60).
        Size = new Vector2(66, 60) * LegacyExperiencePlugin.StableRatio;
    }

    private int currentSelection;
    private ClickableModDisplay[] modDisplays = null!;

    private InputManager inputManager = null!;

    [Resolved]
    private ISkinSource? skin { get; set; }

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
                Action = () => Cycle(shouldBackwards),
            };

            if (i != DisplayIndex)
                display.Alpha = 0;

            modDisplays[i] = display;
            AddInternal(display);
        }

        updateSkin();
        skin?.SourceChanged += updateSkin;
    }


    // stable uses contains instead of checking the activation button,
    // this means when you click with right button previously pressed, the direction will still be backwards.
    // We keep this behaviour in case any user relies on it, but it is not recommended to use right click for mod switching.
    private bool shouldBackwards => inputManager.CurrentState.Mouse.IsPressed(MouseButton.Right);

    private static readonly SampleInfo checkOnSampleInfo = new SampleInfo("UI/check-on");
    private static readonly SampleInfo checkOffSampleInfo = new SampleInfo("UI/check-off");

    private ISample? checkOnSample;
    private ISample? checkOffSample;

    private void updateSkin()
    {
        checkOnSample = skin?.GetSample(checkOnSampleInfo);
        checkOffSample = skin?.GetSample(checkOffSampleInfo);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        inputManager = GetContainingInputManager();
    }

    /// <summary>
    /// Gets the current selection state of the mod switch.
    /// </summary>
    public ModSelectionState State
    {
        get
        {
            if (currentSelection == DisabledSelection)
                return ModSelectionState.Disabled;
            if (currentSelection == NoModSelection)
                return ModSelectionState.NoSelection;
            return ModSelectionState.Selected;
        }
    }

    /// <summary>
    /// Gets information about the current selection state.
    /// </summary>
    public ModSelectionInfo CurrentInfo
    {
        get
        {
            var state = State;
            int? selectedIndex = state == ModSelectionState.Selected ? currentSelection : null;
            LegacyMod? selectedMod = state == ModSelectionState.Selected ? mods[currentSelection] : null;
            return new ModSelectionInfo(state, DisplayIndex, selectedIndex, selectedMod);
        }
    }

    /// <summary>
    /// Represent the current selection of the mod switch. The value is the index of the selected mod in the mods array,
    /// or <see cref="NoModSelection"/> if no mod is selected, or <see cref="DisabledSelection"/> if disabled.
    /// </summary>
    public int CurrentSelection => currentSelection;

    /// <summary>
    /// Represent the index of the mod to be displayed. This is used for display purposes only, and is always in the range of [0, mods.Length - 1]. 
    /// When no mod is selected or disabled, the first mod (index 0) will be displayed.
    /// </summary>
    public int DisplayIndex => currentSelection < mods.Length ? currentSelection : 0;

    /// <summary>
    /// Represent the total number of selections, which is the number of mods plus one.
    /// </summary>
    public int TotalSelections => mods.Length + 1;

    /// <summary>
    /// The currently activated mod. This will be null when no mod is selected or when disabled.
    /// </summary>
    public LegacyMod? SelectedMod => State == ModSelectionState.Selected ? mods[currentSelection] : null;

    /// <summary>
    /// The selection index representing the no selection state. This is always equal to mods.Length.
    /// </summary>
    public int NoModSelection => mods.Length;

    /// <summary>
    /// The selection index representing the disabled state. This is always equal to mods.Length + 1.
    /// </summary>
    public int DisabledSelection => mods.Length + 1;

    /// <summary>
    /// Cycles to the next or previous mod selection.
    /// </summary>
    public void Cycle(bool backwards = false)
    {
        // If disabled, clicking always returns to no selection
        if (State == ModSelectionState.Disabled)
        {
            setSelection(NoModSelection);
            return;
        }

        int direction = backwards ? -1 : 1;
        int newSelection = (currentSelection + direction + TotalSelections) % TotalSelections;

        setSelection(newSelection);
    }

    private void setSelection(int newSelection)
    {
        if (currentSelection == newSelection)
            return;

        var previousInfo = CurrentInfo;
        currentSelection = newSelection;
        var currentInfo = CurrentInfo;

        OnSelectionChanged(previousInfo, currentInfo);
    }

    private void disableMod(ClickableModDisplay mod)
    {
        mod.FadeIn(100)
           .ScaleTo(1f, 400, Easing.OutElastic)
           .RotateTo(0f, 400, Easing.OutElastic)
           .FadeColour(Colour4.White.Darken(0.5f), 400, Easing.OutElastic);
    }

    private void resetMod(ClickableModDisplay mod)
    {
        mod.FadeIn(100)
           .ScaleTo(1f, 400, Easing.OutElastic)
           .RotateTo(0f, 400, Easing.OutElastic)
           .FadeColour(Colour4.White, 400, Easing.OutElastic);
    }

    /// <summary>
    /// Sets the selection to a specific mod by its index.
    /// </summary>
    /// <param name="index">The index of the mod to select (0 to mods.Length - 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void SelectMod(int index)
    {
        if (index < 0 || index >= mods.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {mods.Length - 1}");

        setSelection(index);
    }

    /// <summary>
    /// Clears the current selection, returning to the no selection state.
    /// </summary>
    public void ClearSelection()
    {
        setSelection(mods.Length);
    }

    /// <summary>
    /// Sets the mod switch to the disabled state.
    /// When disabled, the displayed mod is darkened by 50% and cycling will return to no selection.
    /// </summary>
    public void SetDisabled()
    {
        setSelection(DisabledSelection);
    }

    /// <summary>
    /// Gets whether the mod switch is currently in the disabled state.
    /// </summary>
    public bool IsDisabled => State == ModSelectionState.Disabled;

    /// <summary>
    /// Called when the selection state changes. Override to respond to state changes.
    /// </summary>
    /// <param name="previousInfo">Information about the previous selection state.</param>
    /// <param name="currentInfo">Information about the current selection state.</param>
    protected virtual void OnSelectionChanged(ModSelectionInfo previousInfo, ModSelectionInfo currentInfo)
    {
        var sample = (currentInfo.State, previousInfo.State) switch
        {
            (ModSelectionState.Selected, _) => checkOnSample,
            (ModSelectionState.NoSelection, ModSelectionState.Disabled) => null,
            _ => checkOffSample,
        };
        sample?.Play();

        if (previousInfo.DisplayedIndex != currentInfo.DisplayedIndex)
            deactivateMod(modDisplays[previousInfo.DisplayedIndex]);

        switch (currentInfo.State)
        {
            case ModSelectionState.Selected:
                activateMod(modDisplays[currentInfo.DisplayedIndex]);
                break;

            case ModSelectionState.NoSelection:
                resetMod(modDisplays[currentInfo.DisplayedIndex]);
                break;

            case ModSelectionState.Disabled:
                disableMod(modDisplays[currentInfo.DisplayedIndex]);
                break;
        }
    }

    private void activateMod(ClickableModDisplay mod)
    {
        mod.FadeIn(100)
           .ScaleTo(1.2f, 400, Easing.OutElastic)
           .RotateTo(activate_rotation, 400, Easing.OutElastic)
           .FadeColour(Colour4.White, 400, Easing.OutElastic);
    }

    private const float activate_rotation = (float)(0.1 * 180 / Math.PI);

    private void deactivateMod(ClickableModDisplay mod)
    {
        mod.FadeOut(100)
           .ScaleTo(1f, 400, Easing.OutElastic)
           .RotateTo(0f, 400, Easing.OutElastic)
           .FadeColour(Colour4.White, 400, Easing.OutElastic);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        skin?.SourceChanged -= updateSkin;
    }

    private partial class ClickableModDisplay : ClickableContainer, IHasLegacyTooltip
    {
        private readonly LegacyModDisplay modDisplay;

        public ClickableModDisplay(LegacyMod mod)
        {
            Child = modDisplay = new LegacyModDisplay(mod);

            AutoSizeAxes = Axes.Both;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            // in stable, click event is fired immediately,
            // but since clickable container in lazer fires click event on mouse up,
            // we also trigger click on right mouse button to keep the behaviour consistent with left click.
            if (e.Button == MouseButton.Right)
            {
                TriggerClick();
            }
        }

        [BackgroundDependencyLoader]
        private void load(IBindable<Ruleset>? ruleset)
        {
            // Since mod switches will be recreated every time the ruleset changes, we don't handle ruleset changes here. 
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
