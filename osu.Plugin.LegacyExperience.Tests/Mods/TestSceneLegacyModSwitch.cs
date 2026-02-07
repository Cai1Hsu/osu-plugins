using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Skinning;
using osu.Framework.Testing;
using NUnit.Framework;
using osu.Plugin.LegacyExperience.Mods;
using osu.Game;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Framework.Bindables;
using osu.Game.Rulesets;

namespace osu.Plugin.LegacyExperience.Tests.Mods;

public partial class TestSceneLegacyModSwitch : LocalSkinTestScene
{
    private SkinProvidingContainer content = null!;

    [Cached(typeof(IBindable<Ruleset>))]
    private readonly Bindable<Ruleset> currentRuleset = new Bindable<Ruleset>();

    private static readonly RulesetInfo[] rulesets = new[]
    {
        createRulesetInfo(0, "Osu"),
        createRulesetInfo(1, "Taiko"),
        createRulesetInfo(2, "Catch"),
        createRulesetInfo(3, "Mania"),
        createRulesetInfo(-1, "Custom")
    };

    private static RulesetInfo createRulesetInfo(int id, string name) => new RulesetInfo(name, name, "unused", id);

    private void setRuleset(int id) => currentRuleset.Value = new TestRuleset(rulesets.First(r => r.OnlineID == id));

    [BackgroundDependencyLoader]
    private void load(OsuGameBase game)
    {
        // for tooltip localisation testing purposes
        Add(new SettingsEnumDropdown<Language>()
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            AlwaysShowSearchBar = true,
            LabelText = "Game language",
            Current = { BindTarget = game.CurrentLanguage },
        });

        Add(content = new SkinProvidingContainer(new DefaultLegacySkin(this))
        {
            RelativeSizeAxes = Axes.Both,
        });

        // ruleset affects some mods' tooltip text
        AddStep("set ruleset to osu!standard", () => setRuleset(0));
        AddStep("set ruleset to osu!taiko", () => setRuleset(1));
        AddStep("set ruleset to osu!catch", () => setRuleset(2));
        AddStep("set ruleset to osu!mania", () => setRuleset(3));
        AddStep("set ruleset to custom", () => setRuleset(-1));
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("clear", () => content.Clear());
    }

    private void createModSwitch(LegacyMod[] mods)
    {
        content.Add(new LegacyModSwitch(mods)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });
    }

    private static readonly LegacyMod[] singleCombinationMods = new[]
    {
        LegacyMod.Easy,
        LegacyMod.Flashlight,
        LegacyMod.HalfTime,
        LegacyMod.HardRock,
        LegacyMod.Hidden, // HD is single combination is some rulesets
        LegacyMod.KeyCoop,
        LegacyMod.Mirror,
        LegacyMod.Nightcore,
        LegacyMod.NoFail,
        LegacyMod.Perfect,
        LegacyMod.Random,
        LegacyMod.Relax,
        LegacyMod.Relax2,
        LegacyMod.ScoreV2,
        LegacyMod.SpunOut,
        LegacyMod.SuddenDeath,
        LegacyMod.Target,
    };

    [Test]
    public void TestSingleCombinations()
    {
        foreach (var mod in singleCombinationMods)
        {
            AddStep($"add {mod} switch", () =>
            {
                content.Clear();
                createModSwitch(new[] { mod });
            });
        }
    }

    [Test]
    public void TestDTNC()
    {
        AddStep("add DT/NC switch", () =>
        {
            createModSwitch(new[] { LegacyMod.DoubleTime, LegacyMod.Nightcore });
        });
    }

    [Test]
    public void TestATCN()
    {
        AddStep("add AT/CN switch", () =>
        {
            createModSwitch(new[] { LegacyMod.Autoplay, LegacyMod.Cinema });
        });
    }

    [Test]
    public void TestFIHD()
    {
        AddStep("add FI/HD switch(mania)", () =>
        {
            createModSwitch(new[] { LegacyMod.FadeIn, LegacyMod.Hidden });
        });
    }

    [Test]
    public void TestPFSD()
    {
        AddStep("add PF/SD switch", () =>
        {
            createModSwitch(new[] { LegacyMod.Perfect, LegacyMod.SuddenDeath });
        });
    }

    [Test]
    public void TestManiaKeys()
    {
        AddStep("add mania keys switch", () =>
        {
            // initial state is 4K
            var keysMod = Enumerable.Range((int)LegacyMod.Key4, 9 - 4 + 1)
                                    .Concat(Enumerable.Range((int)LegacyMod.Key1, 3))
                                    .Select(static i => (LegacyMod)i)
                                    .ToArray();

            createModSwitch(keysMod);
        });
    }
}
