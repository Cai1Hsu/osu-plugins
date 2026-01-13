using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class TestSceneLegacyPanel : LocalSkinTestScene
{
    private SkinProvidingContainer skinProvidingContainer = null!;
    private LegacyPanel? panel = null;

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

            skinProvidingContainer.Add(panel = new LegacyPanel
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
        });
    }
}
