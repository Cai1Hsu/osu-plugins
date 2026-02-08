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
            });
            dialog.Show();
        });

        AddStep("hide dialog", () => dialog?.Hide());
    }
}
