using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osu.Plugin.Legacy.Leaderboards;

namespace osu.Plugin.Legacy.Tests.Leaderboards;

public partial class TestSceneLegacyLeaderboardEntry : OsuTestScene, IStorageResourceProvider
{
    private DummyAPIAccess api => (DummyAPIAccess)API;

    private APIUser friend = new APIUser
    {
        Id = 2,
        Username = "Friend",
    };

    private APIUser localUser => api.LocalUser.Value;

    private readonly Bindable<int?> ScorePosition = new Bindable<int?>();

    private readonly BindableBool HasQuit = new BindableBool();

    private readonly BindableLong TotalScore = new BindableLong();

    private readonly BindableInt Combo = new BindableInt();

    private LegacyLeaderboardEntry? entry;

    [BackgroundDependencyLoader]
    private void load()
    {
        api.LocalUserState.Friends.Add(new APIRelation()
        {
            TargetUser = friend,
            TargetID = friend.Id,
        });
    }

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
                Child = entry = new LegacyLeaderboardEntry()
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    ScorePosition = { BindTarget = ScorePosition },
                    HasQuit = { BindTarget = HasQuit },
                    TotalScore = { BindTarget = TotalScore },
                    Combo = { BindTarget = Combo },
                }
            };
        });

        AddStep("Set local user", () => updateUser(localUser));

        AddStep("Clear position", () => ScorePosition.Value = null);
        AddSliderStep("Set position", 1, 100, 50, pos => ScorePosition.Value = (int?)pos);

        AddToggleStep("Set quit", value => HasQuit.Value = value);
        AddSliderStep("Set score", 0, 1_000_000, 500_000, score => TotalScore.Value = score);
        AddSliderStep("Set combo", 0, 2000, 500, combo => Combo.Value = combo);

        AddStep("Set friend user", () => updateUser(friend));
        AddStep("Clear user", () => updateUser(null!));

        AddToggleStep("Set tracking", updateTracking);
    }

    private void updateTracking(bool isTracking)
    {
        if (entry is null)
            return;

        entry.IsTracking = isTracking;
        entry.UpdatePanelState();
    }

    private void updateUser(APIUser user)
    {
        if (entry is null)
            return;

        entry.User = user;
        entry.UpdatePanelState();
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
