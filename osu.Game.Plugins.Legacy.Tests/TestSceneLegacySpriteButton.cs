using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Plugins.Legacy.Tests;

public partial class TestSceneLegacySpriteButton : OsuTestScene, IStorageResourceProvider
{
    private SkinProvidingContainer skinContainer = null!;
    private LegacySpriteButton button = null!;

    [Resolved]
    private GameHost host { get; set; } = null!;

    public IRenderer Renderer => host.Renderer;
    public AudioManager AudioManager => Audio;
    public IResourceStore<byte[]> Files => null!;
    public new IResourceStore<byte[]> Resources => base.Resources;
    public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
        => host.CreateTextureLoaderStore(Resources);
    RealmAccess IStorageResourceProvider.RealmAccess => null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("Create skin container", () =>
        {
            Add(skinContainer = new SkinProvidingContainer(new DefaultLegacySkin(this))
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            });
        });

        AddStep("Add button", () =>
        {
            skinContainer.Clear();
            skinContainer.Add(button = new LegacySpriteToggleButton()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                HoverSample = new PoolableSkinnableSample(new SampleInfo("click-short")),
                ClickSample = new PoolableSkinnableSample(new SampleInfo("click-short-confirm")),
                DefaultTexture = "UI/overlay-show",
                ToggledTexture = "UI/overlay-hide",
            });
        });
    }
}
