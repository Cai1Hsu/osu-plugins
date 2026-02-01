using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Tests.Visual;

namespace osu.Plugin.LegacyExperience.Tests;

public abstract partial class LocalSkinTestScene : OsuTestScene, IStorageResourceProvider
{
    /// <summary>
    /// Set this to a valid local skin path to test with a specific skin.
    /// </summary>
    public virtual string? LocalSkinPath => null;

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