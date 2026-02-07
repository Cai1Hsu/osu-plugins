using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Skinning;
using osu.Framework.Testing;
using NUnit.Framework;
using osu.Plugin.LegacyExperience.Mods;
using osu.Game;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;

namespace osu.Plugin.LegacyExperience.Tests.Mods;

public partial class TestSceneLegacyModSwitch : LocalSkinTestScene
{
    private SkinProvidingContainer content = null!;

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
