using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Carousel;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.SongSelect;

namespace osu.Plugin.LegacyExperience.Tests.SongSelect;

public partial class TestSceneLegacyGroupPanel : LocalSkinTestScene
{
    private SkinProvidingContainer skinProvidingContainer = null!;
    private LegacyPanel? panel = null;

    [Cached]
    private LegacyPanelColors panelColors = LegacyPanelColors.CreateDefault();

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create skin container", () =>
        {
            var skin = new DefaultLegacySkin(this);
            Add(skinProvidingContainer = new SkinProvidingContainer(skin));
        });

        AddStep("create panel", () =>
        {
            skinProvidingContainer.Clear();

            skinProvidingContainer.Add(panel = new LegacyGroupPanel
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Item = new CarouselItem("Test group")
            });
        });
    }
}
