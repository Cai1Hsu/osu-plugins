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

namespace osu.Game.Plugins.Skins.Testing;

public abstract partial class LocalSkinTestScene : OsuTestScene, IStorageResourceProvider
{
    public virtual string LocalSkinPath => throw new Exception("Set this to a valid local skin path to test with a specific skin.");

    private IResourceStore<byte[]> resourceStore = null!;

    protected SkinProvidingContainer SkinContainer = null!;

    [SetUpSteps]
    public virtual void SetUpSteps()
    {
        AddStep("Create resource store", () => resourceStore = createResourceStore());

        AddStep("Create skin container", () =>
        {
            Add(SkinContainer = new SkinProvidingContainer(new DefaultLegacySkin(this))
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
        });
    }

    private IResourceStore<byte[]> createResourceStore()
        => new StorageBackedResourceStore(new NativeStorage(LocalSkinPath));

    [Resolved]
    private GameHost host { get; set; } = null!;

    #region IResourceStorageProvider

    public IRenderer Renderer => host.Renderer;
    public AudioManager AudioManager => Audio;
    public IResourceStore<byte[]> Files => Resources;
    public new IResourceStore<byte[]> Resources => resourceStore;
    public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
        => host.CreateTextureLoaderStore(Resources);
    RealmAccess IStorageResourceProvider.RealmAccess => null!;

    #endregion
}
