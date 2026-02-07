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

[Cached(typeof(IModHoverManager))]
public partial class TestSceneLegacyModDisplay : LocalSkinTestScene, IModHoverManager
{
    private SkinProvidingContainer content = null!;

    private bool bypassHoverSampleDebounce;

    [BackgroundDependencyLoader]
    private void load()
    {
        // ModDisplay is typically arranged in a high-density layout within the UI, 
        // making it prone to generating excessive hover samples when the cursor moves.
        // osu!stable debounces all samples with a threshold of 50ms, 
        // we implement the same logic here and provide an option to bypass it for testing purposes.
        AddToggleStep("bypass hover sample debounce", value => bypassHoverSampleDebounce = value);

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

    private const double hoverSampleDebounceTime = 50;
    private double lastHoverSampleTime = double.MinValue;

    bool IModHoverManager.RequestHoverSample()
    {
        if (bypassHoverSampleDebounce)
            return true;

        double currentTime = Time.Current;

        if (currentTime - lastHoverSampleTime < hoverSampleDebounceTime)
            return false;

        lastHoverSampleTime = currentTime;
        return true;
    }
}
