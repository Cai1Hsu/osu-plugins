using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Tests.Visual;
using osu.Framework.Graphics.Shapes;
using osu.Game.IO;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Audio;
using osu.Game.Database;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;
using osu.Framework.Testing;

namespace osu.Plugin.LegacyLeaderboard.Tests;

// TODO: extract local skin test scene
public partial class TestSceneLegacyLeaderboardEntry : OsuTestScene, IStorageResourceProvider
{
    private Box? background = null;
    private LegacyLeaderboardEntry? scoreEntry = null;
    private Random random = new Random();

    public string LocalSkinPath
        // the built-in legacy skin doesn't contain required assets for this skin component
        => throw new Exception("Set this to a valid local skin path to test with a specific skin.");

    [SetUpSteps]
    public void Setup()
    {
        AddStep("create score entry", () =>
        {
            Clear();
            
            var skin = new DefaultLegacySkin(this);

            Children = new Drawable[]
            {
                background = new Box
                {
                    Colour = Colour4.DarkGray,
                    RelativeSizeAxes = Axes.Both,
                },
                new SkinProvidingContainer(skin)
                {
                    Child = scoreEntry = new LegacyLeaderboardEntry()
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        PlayerName = "Peppy",
                    }
                }
            };
        });

        AddSliderStep("set rank", 1, 50000, 1, val =>
        {
            if (scoreEntry != null)
                scoreEntry.Rank = val;
        });
        AddSliderStep("set score", 0, 1_000_000, 0, val =>
        {
            if (scoreEntry != null)
                scoreEntry.Score = val;
        });
        AddSliderStep("set combo", 0, 10_000, 0, val =>
        {
            if (scoreEntry != null)
                scoreEntry.Combo = val;
        });
        AddToggleStep("toggle Pass", val =>
        {
            if (scoreEntry != null)
                scoreEntry.Passing = val;
        });
        AddSliderStep("set index", 0, 8, 0, val =>
        {
            if (scoreEntry != null)
                scoreEntry.Alpha = LegacyLeaderboardBase.GetEntryAlpha(val, 6);
        });
        AddStep("change background", () =>
        {
            if (background is not null)
                background.Colour = new Colour4(
                    random.NextSingle(),
                    random.NextSingle(),
                    random.NextSingle(),
                    1);
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
    public new IResourceStore<byte[]> Resources => createResourceStore();
    public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
        => host.CreateTextureLoaderStore(Resources);
    RealmAccess IStorageResourceProvider.RealmAccess => null!;

    #endregion
}
