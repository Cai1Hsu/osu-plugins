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
using osu.Game.Scoring;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Plugin.LegacyLeaderboard.Tests;

public partial class TestSceneLegacyLeaderboardEntry : OsuTestScene, IStorageResourceProvider
{
    [SetUpSteps]
    public void SetUpSteps()
    {
        var skin = new DefaultLegacySkin(this);

        AddStep("Create entry", () =>
        {
            Child = new SkinProvidingContainer(skin)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                // FIXME: test broken due to dependency of leaderboard and score info.
                Child = new LegacyLeaderboardEntry(new GameplayLeaderboardScore(new ScoreInfo(), true, GameplayLeaderboardScore.ComboDisplayMode.Current))
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }
            };
        });
    }

    [Resolved]
    private GameHost host { get; set; } = null!;

    public IRenderer Renderer => host.Renderer;

    public AudioManager? AudioManager => null;

    public IResourceStore<byte[]> Files => base.Resources;

    public RealmAccess RealmAccess => null!;

    IResourceStore<byte[]> IStorageResourceProvider.Resources => base.Resources;

    public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
        => host.CreateTextureLoaderStore(underlyingStore);
}
