using System.Collections.Frozen;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Plugin.LegacyExperience.Graphics;
using osu.Plugin.LegacyExperience.Localisations;

namespace osu.Plugin.LegacyExperience.Mods;

[Cached(typeof(IModHoverManager))]
public partial class LegacyModSelection : LegacyDialog, IModHoverManager
{
    public FontText MultiplierText { get; private set; } = null!;

    public SelectionGroup ReductionGroup { get; private set; } = null!;

    public SelectionGroup IncreaseGroup { get; private set; } = null!;

    public SelectionGroup SpecialGroup { get; private set; } = null!;

    public LegacyModSelection()
    {
        TitleText.Text = LegacyStrings.ModSelection_Title;

        Content.AddRange(new Drawable[]
        {
            MultiplierText = new FontText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Font = LegacyFont.Default.With(size: 30),
                // match stable's currentVerticalSpace usage
                Margin = new MarginPadding
                {
                    Top = 30 * LegacyExperiencePlugin.StableRatio,
                    Bottom = 9 * LegacyExperiencePlugin.StableRatio,
                },
            },
            ReductionGroup = new SelectionGroup(LegacyModType.Reduction)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Reduction,
                    Colour = Colour4.LimeGreen,
                },
            },
            IncreaseGroup = new SelectionGroup(LegacyModType.Increase)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Increase,
                    Colour = Colour4.OrangeRed,
                },
            },
            SpecialGroup = new SelectionGroup(LegacyModType.Special)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Special,
                    Colour = Colour4.White,
                },
            },
            // match stable's currentVerticalSpace usage to create the same layout margin at the bottom of the dialog.
            Empty().With(d =>
            {
                d.Height = 23 * LegacyExperiencePlugin.StableRatio;
                d.RelativeSizeAxes = Axes.X;
            })
        });
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        AddOption(LegacyStrings.ModSelection_Reset, b =>
        {
            b.BackgroundColour = Colour4.OrangeRed;
            b.Action = () => selectedMods.Value = Array.Empty<Mod>();
        });
        AddOption(LegacyStrings.General_Close, b =>
        {
            b.BackgroundColour = Colour4.Gray;
            b.Action = Close;
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
            registerModSettingsChange();
        }, true);

        localScoreMultiplier.BindValueChanged(_ => updateMultiplierText(), true);
    }

    private void updateModsInformation()
    {
        double multiplier = 1.0;

        // TODO:
        // there are many mods that's not supported in legacy mod selection, but they may still change multiplier
        // We have to find a way to notify the user about the inconsistency of the multiplier,
        // otherwise they may be confused about why the multiplier doesn't match their expectation.
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
        var groups = Content.OfType<SelectionGroup>().ToArray();

        foreach (var group in groups)
        {
            group.Mods.Clear();

            var availableMods = localAvailableMods.Value[(int)group.GroupType];
            var order = displayOrders[group.GroupType];
            populateGroup(group, availableMods, order);
        }
    }

    private void populateGroup(SelectionGroup group, Dictionary<LegacyMod, Mod> availableMods, IEnumerable<LegacyMod[]> order)
    {
        var modInfos = new List<ModInfo>();

        foreach (var combination in order)
        {
            modInfos.Clear();

            foreach (var legacyMod in combination)
            {
                if (availableMods.TryGetValue(legacyMod, out var mod))
                    modInfos.Add(new ModInfo(legacyMod, mod));
            }

            addToGroup(group, modInfos);
        }
    }

    private readonly record struct ModInfo(LegacyMod LegacyMod, Mod Mod);

    private void addToGroup(SelectionGroup group, IReadOnlyList<ModInfo> combinations)
    {
        if (combinations.Count == 0)
            return;

        var modSwitch = combinations switch
        {
            [ModInfo(LegacyMod.ScoreV2, _)] => new ScoreV2ModSwitch(combinations),
            _ => new UserModSwitch(combinations),
        };

        group.Mods.Add(modSwitch);
    }

    private void registerModSettingsChange()
    {
        Debug.Assert(modSettingChangeTracker is not null);

        var modSwitches = Content.OfType<SelectionGroup>()
            .SelectMany(static g => g.Mods.OfType<UserModSwitch>());

        foreach (var modSwitch in modSwitches)
            modSettingChangeTracker.SettingChanged += _ => modSwitch.OnSettingChanged();
    }

    #region  Combinations and display order

    private static readonly LegacyMod[] combination_SDPF = new[] { LegacyMod.SuddenDeath, LegacyMod.Perfect };
    private static readonly LegacyMod[] combination_DTNC = new[] { LegacyMod.DoubleTime, LegacyMod.Nightcore };
    private static readonly LegacyMod[] combination_FIHD = new[] { LegacyMod.FadeIn, LegacyMod.Hidden };
    private static readonly LegacyMod[] combination_ATCN = new[] { LegacyMod.Autoplay, LegacyMod.Cinema };
    private static readonly LegacyMod[] combination_KEYN = Enumerable.Range((int)LegacyMod.Key4, 9 - 4 + 1)
                                                                     .Concat(Enumerable.Range((int)LegacyMod.Key1, 3))
                                                                     .Select(i => (LegacyMod)i)
                                                                     .ToArray();

    private static readonly LegacyMod[][] reductionOrder = new[]
    {
        new [] { LegacyMod.Easy },
        new [] { LegacyMod.NoFail },
        new [] { LegacyMod.HalfTime },
    };

    private static readonly LegacyMod[][] increaseOrder = new[]
    {
        new [] { LegacyMod.HardRock },
        combination_SDPF,
        combination_DTNC,
        combination_FIHD,
        new [] { LegacyMod.Flashlight },
    };

    private static readonly LegacyMod[][] specialOrder = new[]
    {
        combination_KEYN,
        new [] { LegacyMod.KeyCoop },
        new [] { LegacyMod.Mirror },
        new [] { LegacyMod.Random },
        new [] { LegacyMod.Relax },
        new [] { LegacyMod.Relax2 },
        new [] { LegacyMod.Target },
        new [] { LegacyMod.SpunOut },
        combination_ATCN,
        new [] { LegacyMod.ScoreV2 },
    };

    private static readonly FrozenDictionary<LegacyModType, LegacyMod[][]> displayOrders = new Dictionary<LegacyModType, LegacyMod[][]>()
    {
        [LegacyModType.Reduction] = reductionOrder,
        [LegacyModType.Increase] = increaseOrder,
        [LegacyModType.Special] = specialOrder,
    }.ToFrozenDictionary();

    #endregion

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
