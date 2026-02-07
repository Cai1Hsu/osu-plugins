using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Mods;

namespace osu.Plugin.LegacyExperience.Tests.Mods;

public partial class TestSceneLegacyModSelection : LocalSkinTestScene
{
    private SkinProvidingContainer content = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(content = new SkinProvidingContainer(new DefaultLegacySkin(this))
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [SetUpSteps]
    public void SetupSteps()
    {
        AddStep("clear", () => content.Clear());
    }

    [Test]
    public void TestModsPositioning()
    {
        LegacyDialog? dialog = null;

        AddStep("add dialog", () =>
        {
            content.Clear();
            content.Add(dialog = new LegacyModSelection()
            {
                MultiplierText = { Text = "Score Multiplier: 1.00x" },
                ReductionGroup =
                {
                    Mods =
                    {
                        Children = new Drawable[]
                        {
                            new LegacyModSwitch(new[] { LegacyMod.Easy }),
                            new LegacyModSwitch(new[] { LegacyMod.NoFail }),
                            new LegacyModSwitch(new[] { LegacyMod.HalfTime }),
                        }
                    }
                },
                IncreaseGroup =
                {
                    Mods =
                    {
                        Children = new Drawable[]
                        {
                            new LegacyModSwitch(new[] { LegacyMod.HardRock }),
                            new LegacyModSwitch(new[] { LegacyMod.SuddenDeath, LegacyMod.Perfect }),
                            new LegacyModSwitch(new[] { LegacyMod.DoubleTime, LegacyMod.Nightcore }),
                            new LegacyModSwitch(new[] { LegacyMod.Hidden }),
                            new LegacyModSwitch(new[] { LegacyMod.Flashlight }),
                        }
                    }
                },
                SpecialGroup =
                {
                    Mods =
                    {
                        Children = new Drawable[]
                        {
                            new LegacyModSwitch(new[] { LegacyMod.Relax }),
                            new LegacyModSwitch(new[] { LegacyMod.Relax2 }),
                            new LegacyModSwitch(new[] { LegacyMod.Target }),
                            new LegacyModSwitch(new[] { LegacyMod.SpunOut }),
                            new LegacyModSwitch(new[] { LegacyMod.Autoplay, LegacyMod.Cinema }),
                            new LegacyModSwitch(new[] { LegacyMod.ScoreV2 }),
                        }
                    }
                }
            });
            dialog.Show();
        });

        AddStep("hide dialog", () => dialog?.Hide());
    }
}
