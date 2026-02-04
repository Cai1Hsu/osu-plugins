using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.SongSelect;

namespace osu.Plugin.LegacyExperience.Tests.SongSelect;

public partial class TestSceneStarDifficultyDisplay : LocalSkinTestScene
{
    private double starDifficulty;
    private SkinProvidingContainer skinContainer = null!;
    private StarDifficultyDisplay? starDifficultyDisplay = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        var skin = new DefaultLegacySkin(this);

        AddRange(new Drawable[]
        {
            skinContainer = new SkinProvidingContainer(skin)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            }
        });
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("Create star difficulty display", () =>
        {
            skinContainer.Clear(true);
            skinContainer.Add(starDifficultyDisplay = new StarDifficultyDisplay()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Current = { Value = starDifficulty }
            });
        });

        AddSliderStep("Set star difficulty", 0.0, 15.0, 5.0, v =>
        {
            starDifficulty = v;

            starDifficultyDisplay?.Current.Value = starDifficulty;
        });
    }
}