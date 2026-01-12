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

public partial class TestSceneLegacyBreakOverlayDrawable : OsuTestScene, IStorageResourceProvider
{
    private LegacyBreakOverlayDrawable legacyBreakOverlay = null!;
    private int animationLoopCount = 1;

    public string? LocalSkinPath
        // TODO: Set this to a valid local skin path to test with a specific skin.
        => null;

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

    private IResourceStore<byte[]> createResourceStore()
        => LocalSkinPath is null ? base.Resources : new StorageBackedResourceStore(new NativeStorage(LocalSkinPath));

    [Resolved]
    private GameHost host { get; set; } = null!;

    #region IResourceStorageProvider

    public IRenderer Renderer => host.Renderer;
    public AudioManager AudioManager => Audio;
    public IResourceStore<byte[]> Files => Resources;
    public new IResourceStore<byte[]> Resources => createResourceStore();
    public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
        => LocalSkinPath is null ? host.CreateTextureLoaderStore(underlyingStore) : host.CreateTextureLoaderStore(Resources);
    RealmAccess IStorageResourceProvider.RealmAccess => null!;

    #endregion
}
