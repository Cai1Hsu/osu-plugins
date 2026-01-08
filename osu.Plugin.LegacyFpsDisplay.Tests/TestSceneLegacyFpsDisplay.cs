using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Plugin.LegacyFpsDisplay.Tests;

public partial class TestSceneLegacyFpsDisplay : OsuTestScene, IStorageResourceProvider
{
    [Resolved]
    private FontStore fontStore { get; set; } = null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("Create fps display", () =>
        {
            var skin = new DefaultLegacySkin(this);

            Child = new SkinProvidingContainer(skin)
            {
                Child = new LegacyFpsDisplay
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(200, 100),
                }
            };
        });
    }


    #region IResourceStorageProvider

    [Resolved]
    private GameHost host { get; set; } = null!;

    public IRenderer Renderer => host.Renderer;
    public AudioManager AudioManager => Audio;
    public IResourceStore<byte[]> Files => null!;
    public new IResourceStore<byte[]> Resources => base.Resources;
    public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => host.CreateTextureLoaderStore(underlyingStore);
    RealmAccess IStorageResourceProvider.RealmAccess => null!;

    #endregion
}
