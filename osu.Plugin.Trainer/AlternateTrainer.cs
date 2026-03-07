using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Plugin.Trainer;

/// <summary>
/// An input trainer for the Alternate mod rule: never use the same key twice in a row.
/// After each key press, flashes the side corresponding to the expected next key.
/// </summary>
public partial class AlternateTrainer : InputTrainer, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    protected override OsuAction? GetExpectedAction() => LastAcceptedAction switch
    {
        OsuAction.LeftButton => OsuAction.RightButton,
        OsuAction.RightButton => OsuAction.LeftButton,
        _ => null,
    };

    [SettingSource("Enable when no alternate mod", "Show the trainer even when no alternate mod is applied.")]
    public BindableBool EnableWhenNoAlternateMod { get; } = new BindableBool(false);

    private bool hasAlternateMod = false;

    [BackgroundDependencyLoader]
    private void load(DrawableRuleset drawableRuleset)
    {
        hasAlternateMod = drawableRuleset.Mods.OfType<OsuModAlternate>().Any();
    }

    protected override bool ShouldFlash => EnableWhenNoAlternateMod.Value || hasAlternateMod;
}
