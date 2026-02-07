using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Mods;
using osuTK;

namespace osu.Plugin.LegacyExperience.Tests.Mods;

public partial class TestSceneLegacyModDisplay : LocalSkinTestScene
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
    public void TestDisplay()
    {
        AddStep("add mods", () =>
        {
            var fillFlow = new FillFlowContainer()
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
            };

            content.Add(new OsuScrollContainer(Direction.Vertical)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Y,
                ScrollbarOverlapsContent = false,
                Width = 100,
                Child = fillFlow,
            });

            var mods = Enum.GetValues<LegacyMod>();

            foreach (var mod in mods)
            {
                fillFlow.Add(new LegacyModDisplay(mod)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }
        });
    }
}
