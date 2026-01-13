using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Gameplay;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.Tests.Gameplay;

public partial class TestSceneLegacyBreakOverlayDrawable : LocalSkinTestScene
{
    private LegacyBreakOverlayDrawable legacyBreakOverlay = null!;
    private int animationLoopCount = 1;

    [SetUpSteps]
    public virtual void SetUpSteps()
    {
        AddStep("Setup components", SetupComponents);

        AddStep("Clear all animations", () => legacyBreakOverlay.ClearAnimations());
        AddStep("Clear warning animation", () => legacyBreakOverlay.ClearWarningArrowsAnimation());

        AddStep("Play passing animation", () => legacyBreakOverlay.PlayBreakRankingAnimation(true));
        AddStep("Play failing animation", () => legacyBreakOverlay.PlayBreakRankingAnimation(false));

        AddSliderStep("Animation loops", 1, 10, 2, v => animationLoopCount = v);
        AddStep("Play warning animation", () => legacyBreakOverlay.PlayWarningAnimation(animationLoopCount));
    }

    protected virtual void SetupComponents()
    {
        var skin = new DefaultLegacySkin(this);

        Children = new Drawable[]
        {
            new Box
            {
                Colour = Color4.Black,
                RelativeSizeAxes = Axes.Both,
            },
            new SkinProvidingContainer(skin)
            {
                Child = legacyBreakOverlay = new LegacyBreakOverlayDrawable(),
            }
        };
    }
}
