using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Models;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;
using osu.Game.Tests.Visual.Metadata;
using osu.Game.Users;
using osu.Plugin.LegacyExperience.Online;

namespace osu.Plugin.LegacyExperience.Tests.Online;

public partial class TestSceneLegacyUserPanel : OsuTestScene
{
    private LocalUserStatisticsProvider statisticsProvider = null!;
    private TestMetadataClient metadataClient = null!;

    private readonly BindableBool showLevelBar = new BindableBool();

    [BackgroundDependencyLoader]
    private void load()
    {
        AddToggleStep("toggle level bar", v => showLevelBar.Value = v);

        var beatmap = createBeatmapInfo("Title", "Artist", "Mapper", "Difficulty");
        var scoreInfo = new ScoreInfo(beatmap, realmUser: new RealmUser() { Username = "Player" });

        AddStep("set online", () => applyAll(p => p with { Status = UserStatus.Online }));
        AddStep("set offline", () => applyAll(p => p with { Status = UserStatus.Offline }));
        AddStep("set dnd", () => applyAll(p => p with { Status = UserStatus.DoNotDisturb }));
        AddStep("set choosing", () => applyAll(p => p with { Activity = new UserActivity.ChoosingBeatmap() }));
        AddStep("set in solo", () => applyAll(p => p with
        {
            Activity = new UserActivity.InSoloGame(beatmap, new OsuRuleset().RulesetInfo)
        }));
        AddStep("set watch playing", () => applyAll(p => p with { Activity = new UserActivity.WatchingReplay(scoreInfo) }));
        AddStep("set spectate playing", () => applyAll(p => p with
        {
            Activity = new UserActivity.SpectatingUser(scoreInfo)
        }));
        AddStep("set SearchingForLobby", () => applyAll(p => p with { Activity = new UserActivity.SearchingForLobby() }));
        AddStep("set InLobby", () => applyAll(p => p with { Activity = new UserActivity.InLobby() }));
        AddStep("set InMultiplayerGame", () => applyAll(p => p with
        {
            Activity = new UserActivity.InMultiplayerGame(beatmap, new OsuRuleset().RulesetInfo)
        }));
        AddStep("set SpectatingMultiplayerGame", () => applyAll(p => p with
        {
            Activity = new UserActivity.SpectatingMultiplayerGame(beatmap, new OsuRuleset().RulesetInfo)
        }));
        AddStep("set ModdingBeatmap", () => applyAll(p => p with { Activity = new UserActivity.ModdingBeatmap(beatmap) }));
        AddStep("set EditingBeatmap", () => applyAll(p => p with { Activity = new UserActivity.EditingBeatmap(beatmap) }));
        AddStep("set TestingBeatmap", () => applyAll(p => p with { Activity = new UserActivity.TestingBeatmap(beatmap) }));
        AddStep("set InDailyChallengeLobby", () => applyAll(p => p with { Activity = new UserActivity.InDailyChallengeLobby() }));
        AddStep("set PlayingDailyChallenge", () => applyAll(p => p with { Activity = new UserActivity.PlayingDailyChallenge(beatmap, new OsuRuleset().RulesetInfo) }));
        AddStep("set no activity", () => applyAll(p => p with { Activity = null }));
    }

    private BeatmapInfo createBeatmapInfo(string title, string artist, string mapper, string difficulty)
    {
        return new BeatmapInfo
        {
            OnlineID = -1,
            Metadata = new BeatmapMetadata
            {
                Title = title,
                Artist = artist,
                Author = new RealmUser
                {
                    Username = mapper,
                }
            },
            DifficultyName = difficulty,
        };
    }

    private void applyAll(Func<UserPresence, UserPresence> transform)
    {
        if (metadataClient is null)
            return;

        foreach (var id in metadataClient.UserPresences.Keys)
            ((BindableDictionary<int, UserPresence>)metadataClient.UserPresences)[id] = transform(metadataClient.UserPresences[id]);
    }

    [SetUp]
    public void Setup() => Schedule(() => Child = new DependencyProvidingContainer
    {
        RelativeSizeAxes = Axes.Both,
        CachedDependencies =
        [
            (typeof(LocalUserStatisticsProvider), statisticsProvider = new LocalUserStatisticsProvider()),
            (typeof(MetadataClient), metadataClient = new TestMetadataClient()
                .With(d => d.BeginWatchingUserPresence())
                .With(d => ((BindableDictionary<int, UserPresence>)d.UserPresences).Add(14546074, new UserPresence
                {
                    Status = UserStatus.Online,
                    Activity = null,
                }))),
        ],
        Children = new Drawable[]
        {
            statisticsProvider,
            metadataClient,
            new LegacyUserPanel(new APIUser
            {
                Username = "Caiyi",
                Id = 14546074,
                CountryCode = CountryCode.CN,
                PlayMode = "osu",
                Statistics = new UserStatistics
                {
                    RankedScore = 10364538038,
                    TotalScore = 45780254271,
                    PP = 4625,
                    Accuracy = 98.06,
                    GlobalRank = 122970,
                    PlayCount = 25677,
                    Level = new UserStatistics.LevelInfo
                    {
                        Current = 100,
                        Progress = 18,
                    }
                },
                SupportLevel = 2,
            }) { Anchor = Anchor.Centre, Origin = Anchor.Centre, ExtendedStyle = { BindTarget = showLevelBar } },
        }
    });
}
