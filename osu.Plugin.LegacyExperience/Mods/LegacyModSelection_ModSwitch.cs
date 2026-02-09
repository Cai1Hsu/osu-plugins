using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Mods;

namespace osu.Plugin.LegacyExperience.Mods;

partial class LegacyModSelection
{

    private partial class UserModSwitch : LegacyModSwitch
    {
        private readonly Type[] mods;

        public UserModSwitch(IReadOnlyList<ModInfo> mods)
            : base(mods.Select(static m => m.LegacyMod).ToArray())
        {
            this.mods = mods.Select(static m => m.Mod.GetType()).ToArray();
        }

        [Resolved]
        protected Bindable<IReadOnlyList<Mod>> SelectedMods { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            SelectedMods.BindValueChanged(_ => onModsChanged(), true);
        }

        private void onModsChanged()
        {
            var selectedModIndex = getSelectedModIndex();

            var mod = selectedModIndex < 0 ? null : SelectedMods.Value[selectedModIndex];
            UpdateSelection(mod);
        }

        protected virtual void UpdateSelection(Mod? mod)
        {
            switch (mod)
            {
                case null:
                    ClearSelection();
                    break;

                case { } when !mod.UsesDefaultConfiguration:
                    SetDisabled();
                    break;

                default:
                    var selectionIndex = Array.FindIndex(mods, m => m.IsInstanceOfType(mod));
                    Debug.Assert(selectionIndex > -1);
                    SelectMod(selectionIndex);
                    break;
            }
        }

        private int getSelectedModIndex()
        {
            var selectedModsList = SelectedMods.Value;
            for (int i = 0; i < selectedModsList.Count; i++)
            {
                if (mods.Any(m => m.IsInstanceOfType(selectedModsList[i])))
                    return i;
            }
            return -1;
        }

        public void OnSettingChanged()
        {
            var selectedModIndex = getSelectedModIndex();

            if (selectedModIndex == -1)
                return;

            var mod = SelectedMods.Value[selectedModIndex];
            UpdateSelection(mod);
        }

        protected override void OnSelectionChanged(ModSelectionInfo previousInfo, ModSelectionInfo currentInfo)
        {
            base.OnSelectionChanged(previousInfo, currentInfo);

            UpdateModSelection(previousInfo, currentInfo);
        }

        protected virtual void UpdateModSelection(ModSelectionInfo previousInfo, ModSelectionInfo currentInfo)
        {
            if (currentInfo.State is ModSelectionState.Disabled)
                return;

            var selecteds = SelectedMods.Value.ToList();

            selecteds.RemoveAll(m => mods.Contains(m.GetType()));

            if (currentInfo.SelectedIndex is { } selectedIndex)
            {
                var modType = mods[selectedIndex];

                selecteds.RemoveAll(m => m.IncompatibleMods.Any(t => t.IsAssignableFrom(modType)));
                selecteds.Add(CreateModInstance(modType));
            }

            SelectedMods.Value = selecteds.ToArray();
        }

        protected Mod CreateModInstance(Type type) => (Mod)Activator.CreateInstance(type)!;
    }

    private partial class ScoreV2ModSwitch : UserModSwitch
    {
        private readonly Type classicModType;

        public ScoreV2ModSwitch(IReadOnlyList<ModInfo> mods)
            : base(mods)
        {
            classicModType = mods.Single().Mod.GetType();
        }

        protected override void UpdateModSelection(ModSelectionInfo previousInfo, ModSelectionInfo currentInfo)
        {
            if (currentInfo.State is ModSelectionState.Disabled)
                return;

            var selecteds = SelectedMods.Value.ToList();

            selecteds.RemoveAll(m => m is ModClassic || m.IncompatibleMods.Any(t => t.IsAssignableFrom(classicModType)));

            // When scoreV2 is not selected, add classic mod if not exists.
            if (currentInfo.State is ModSelectionState.NoSelection)
                selecteds.Add(CreateModInstance(classicModType));

            SelectedMods.Value = selecteds.ToArray();
        }

        protected override void UpdateSelection(Mod? mod)
        {
            Debug.Assert(mod is ModClassic || mod is null);

            switch (mod)
            {
                case null:
                    SelectMod(0);
                    break;

                case { } when !mod.UsesDefaultConfiguration:
                    SetDisabled();
                    break;

                default:
                    ClearSelection();
                    break;
            }
        }
    }
}
