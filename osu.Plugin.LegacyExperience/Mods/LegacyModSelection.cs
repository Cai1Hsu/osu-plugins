using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Plugin.LegacyExperience.Localisations;

namespace osu.Plugin.LegacyExperience.Mods;

[Cached(typeof(IModHoverManager))]
public partial class LegacyModSelection : LegacyDialog, IModHoverManager
{
    public OsuSpriteText MultiplierText { get; private set; } = null!;

    public SelectionGroup ReductionGroup { get; private set; } = null!;

    public SelectionGroup IncreaseGroup { get; private set; } = null!;

    public SelectionGroup SpecialGroup { get; private set; } = null!;

    public LegacyModSelection()
    {
        TitleText.Text = LegacyStrings.ModSelection_Title;

        Content.AddRange(new Drawable[]
        {
            MultiplierText = new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Font = OsuFont.Default.With(size: 30f * LegacyExperiencePlugin.StableRatio),
                // match stable's currentVerticalSpace usage
                Margin = new MarginPadding
                {
                    Top = 30 * LegacyExperiencePlugin.StableRatio,
                    Bottom = 9 * LegacyExperiencePlugin.StableRatio,
                },
            },
            ReductionGroup = new SelectionGroup
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Reduction,
                    Colour = Colour4.LimeGreen,
                },
            },
            IncreaseGroup = new SelectionGroup
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Increase,
                    Colour = Colour4.OrangeRed,
                },
            },
            SpecialGroup = new SelectionGroup
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Special,
                    Colour = Colour4.White,
                },
            },
        });
    }

    private readonly BindableDouble localScoreMultiplier = new BindableDouble(1.0);

    private ModSettingChangeTracker? modSettingChangeTracker;

    [Resolved]
    private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

    private readonly IBindable<Dictionary<ModType, IReadOnlyList<Mod>>> globalAvailableMods = new Bindable<Dictionary<ModType, IReadOnlyList<Mod>>>();
    private readonly Bindable<Dictionary<LegacyMod, Mod>[]> localAvailableMods = new Bindable<Dictionary<LegacyMod, Mod>[]>();

    [BackgroundDependencyLoader]
    private void load(OsuGameBase gameBase)
    {
        globalAvailableMods.BindTo(gameBase.AvailableMods);
        globalAvailableMods.BindValueChanged(_ => computeLocalAvailableMods(), true);
        localAvailableMods.BindValueChanged(_ => updateModGroups(), true);

        selectedMods.BindValueChanged(mods =>
        {
            updateModsInformation();

            modSettingChangeTracker?.Dispose();
            modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
            modSettingChangeTracker.SettingChanged += _ => updateModsInformation();
        }, true);

        localScoreMultiplier.BindValueChanged(_ => updateMultiplierText(), true);
    }

    private void updateModsInformation()
    {
        double multiplier = 1.0;

        // TODO:
        // there are many mods that's not supported in legacy mod selection, but they may still change multiplier
        // We have to find a way to notify the user about the inconsistency of the multiplier,
        // otherwise they may be confused about why the multiplier doesn't their expectation.
        foreach (var mod in selectedMods.Value)
        {
            // matches stable's behaviour: if any unranked mod is selected, the multiplier will be 0.
            if (!mod.Ranked)
            {
                multiplier = 0;
                break;
            }

            multiplier *= mod.ScoreMultiplier;
        }

        localScoreMultiplier.Value = multiplier;
    }

    private void updateMultiplierText()
    {
        var multiplier = localScoreMultiplier.Value;

        // match stable: MultiplierText is not localised.
        MultiplierText.Text = $"Score Multiplier: {multiplier:0.00}x";

        var colour = multiplier switch
        {
            > 1.0 => Colour4.GreenYellow,
            < 1.0 => Colour4.OrangeRed,
            _ => Colour4.White,
        };

        MultiplierText.FadeColour(colour, 400);
    }

    private void computeLocalAvailableMods()
    {
        var localMods = new Dictionary<LegacyMod, Mod>[]
        {
            new Dictionary<LegacyMod, Mod>(), // reduction
            new Dictionary<LegacyMod, Mod>(), // increase
            new Dictionary<LegacyMod, Mod>(), // special
        };

        foreach (var mod in globalAvailableMods.Value
            .SelectMany(static kv => kv.Value)
            .SelectMany(m => m is MultiMod multi ? multi.Mods : new[] { m }))
        {
            switch (mod)
            {
                // We treat ScoreV2 as the reversal of Classic mod, 
                // which means when ScoreV2 is present, Classic mod is not available, and vice versa.
                case ModClassic:
                    localMods[(int)LegacyModType.Special][LegacyMod.ScoreV2] = mod;
                    break;

                case { } when LegacyModExtensions.TryGetLegacyMod(mod, out var legacyMod):
                    var modType = legacyMod.Value.GetModType();
                    localMods[(int)modType][legacyMod.Value] = mod;
                    break;
            }
        }

        localAvailableMods.Value = localMods;
    }

    private void updateModGroups()
    {
        foreach (var group in Content.OfType<SelectionGroup>())
            group.Mods.Clear();

        populateReductionMods();
        populateIncreaseMods();
        populateSpecialMods();
    }

    private readonly record struct ModInfo(LegacyMod LegacyMod, Mod Mod);

    private void populateReductionMods()
    {
        var mods = localAvailableMods.Value[(int)LegacyModType.Reduction];

        var ez_comb = new List<ModInfo>();
        var nf_comb = new List<ModInfo>();
        var ht_comb = new List<ModInfo>();

        if (mods.TryGetValue(LegacyMod.Easy, out var easy))
            ez_comb.Add(new ModInfo(LegacyMod.Easy, easy));

        if (mods.TryGetValue(LegacyMod.NoFail, out var noFail))
            nf_comb.Add(new ModInfo(LegacyMod.NoFail, noFail));

        if (mods.TryGetValue(LegacyMod.HalfTime, out var halfTime))
            ht_comb.Add(new ModInfo(LegacyMod.HalfTime, halfTime));

        addToGroup(ReductionGroup, ez_comb);
        addToGroup(ReductionGroup, nf_comb);
        addToGroup(ReductionGroup, ht_comb);
    }

    private void populateIncreaseMods()
    {
        var mods = localAvailableMods.Value[(int)LegacyModType.Increase];

        var hr_comb = new List<ModInfo>();
        var sd_comb = new List<ModInfo>();
        var dt_comb = new List<ModInfo>();
        var fi_comb = new List<ModInfo>();
        var fl_comb = new List<ModInfo>();

        if (mods.TryGetValue(LegacyMod.HardRock, out var hardRock))
            hr_comb.Add(new ModInfo(LegacyMod.HardRock, hardRock));

        if (mods.TryGetValue(LegacyMod.SuddenDeath, out var suddenDeath))
            sd_comb.Add(new ModInfo(LegacyMod.SuddenDeath, suddenDeath));
        if (mods.TryGetValue(LegacyMod.Perfect, out var perfect))
            sd_comb.Add(new ModInfo(LegacyMod.Perfect, perfect));

        if (mods.TryGetValue(LegacyMod.DoubleTime, out var doubleTime))
            dt_comb.Add(new ModInfo(LegacyMod.DoubleTime, doubleTime));
        if (mods.TryGetValue(LegacyMod.Nightcore, out var nightcore))
            dt_comb.Add(new ModInfo(LegacyMod.Nightcore, nightcore));

        if (mods.TryGetValue(LegacyMod.FadeIn, out var fadeIn))
            fi_comb.Add(new ModInfo(LegacyMod.FadeIn, fadeIn));
        if (mods.TryGetValue(LegacyMod.Hidden, out var hidden))
            fi_comb.Add(new ModInfo(LegacyMod.Hidden, hidden));

        if (mods.TryGetValue(LegacyMod.Flashlight, out var flashlight))
            fl_comb.Add(new ModInfo(LegacyMod.Flashlight, flashlight));

        addToGroup(IncreaseGroup, hr_comb);
        addToGroup(IncreaseGroup, sd_comb);
        addToGroup(IncreaseGroup, dt_comb);
        addToGroup(IncreaseGroup, fi_comb);
        addToGroup(IncreaseGroup, fl_comb);
    }

    private void populateSpecialMods()
    {
        var mods = localAvailableMods.Value[(int)LegacyModType.Special];

        var keyN_comb = new List<ModInfo>();
        var coop_comb = new List<ModInfo>();
        var mirror_comb = new List<ModInfo>();
        var random_comb = new List<ModInfo>();
        var rx_comb = new List<ModInfo>();
        var ap_comb = new List<ModInfo>();
        var tp_comb = new List<ModInfo>();
        var so_comb = new List<ModInfo>();
        var at_comb = new List<ModInfo>();
        var sv2_comb = new List<ModInfo>();

        foreach (var legacyKeyN in Enumerable.Range((int)LegacyMod.Key4, 9 - 4 + 1)
            .Concat(Enumerable.Range((int)LegacyMod.Key1, 3))
            .Select(static i => (LegacyMod)i))
        {
            if (mods.TryGetValue(legacyKeyN, out var keyN))
                keyN_comb.Add(new ModInfo(legacyKeyN, keyN));
        }

        if (mods.TryGetValue(LegacyMod.KeyCoop, out var coop))
            coop_comb.Add(new ModInfo(LegacyMod.KeyCoop, coop));

        if (mods.TryGetValue(LegacyMod.Mirror, out var mirror))
            mirror_comb.Add(new ModInfo(LegacyMod.Mirror, mirror));

        if (mods.TryGetValue(LegacyMod.Random, out var random))
            random_comb.Add(new ModInfo(LegacyMod.Random, random));

        if (mods.TryGetValue(LegacyMod.Relax, out var relax))
            rx_comb.Add(new ModInfo(LegacyMod.Relax, relax));

        if (mods.TryGetValue(LegacyMod.Relax2, out var autopilot))
            ap_comb.Add(new ModInfo(LegacyMod.Relax2, autopilot));

        if (mods.TryGetValue(LegacyMod.Target, out var target))
            tp_comb.Add(new ModInfo(LegacyMod.Target, target));

        if (mods.TryGetValue(LegacyMod.SpunOut, out var spunOut))
            so_comb.Add(new ModInfo(LegacyMod.SpunOut, spunOut));

        if (mods.TryGetValue(LegacyMod.Autoplay, out var autoplay))
            at_comb.Add(new ModInfo(LegacyMod.Autoplay, autoplay));
        if (mods.TryGetValue(LegacyMod.Cinema, out var cinema))
            at_comb.Add(new ModInfo(LegacyMod.Cinema, cinema));

        if (mods.TryGetValue(LegacyMod.ScoreV2, out var scoreV2))
            sv2_comb.Add(new ModInfo(LegacyMod.ScoreV2, scoreV2));

        addToGroup(SpecialGroup, keyN_comb);
        addToGroup(SpecialGroup, coop_comb);
        addToGroup(SpecialGroup, mirror_comb);
        addToGroup(SpecialGroup, random_comb);
        addToGroup(SpecialGroup, rx_comb);
        addToGroup(SpecialGroup, ap_comb);
        addToGroup(SpecialGroup, tp_comb);
        addToGroup(SpecialGroup, so_comb);
        addToGroup(SpecialGroup, at_comb);
        addToGroup(SpecialGroup, sv2_comb);
    }

    private void addToGroup(SelectionGroup group, List<ModInfo> combinations)
    {
        if (combinations.Count == 0)
            return;

        // FIXME: currently for displaying only.
        var comb = combinations.Select(c => c.LegacyMod).ToArray();
        group.Mods.Add(new LegacyModSwitch(comb));
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        modSettingChangeTracker?.Dispose();
    }

    private double lastHoverSampleTime = double.MinValue;
    private const double hoverSampleDebounceTime = 50;

    bool IModHoverManager.RequestHoverSample()
    {
        double currentTime = Time.Current;

        if (currentTime - lastHoverSampleTime < hoverSampleDebounceTime)
            return false;

        lastHoverSampleTime = currentTime;
        return true;
    }
}
