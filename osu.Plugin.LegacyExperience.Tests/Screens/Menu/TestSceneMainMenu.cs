using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Screens.Menu;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneMainMenu : TestSceneWithBeatmap
{
    [BackgroundDependencyLoader]
    private void load()
    {
        Add(new MainMenu
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });
    }

    [Test]
    public void TestUser()
    {
        var dummyAPI = (DummyAPIAccess)API;
        var localUser = dummyAPI.LocalUser;
        var testUser = localUser.Value;

        AddStep("set test user", () => localUser.Value = testUser);

        AddStep("set online user", () => localUser.Value = new APIUser
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
            IsSupporter = true,
            SupportLevel = 2,
        });

        AddStep("set bat user", () => localUser.Value = new APIUser
        {
            IsQAT = true,
            IsSupporter = true,
        });

        AddStep("log out", () => dummyAPI.Logout());
    }

    [Test]
    public void TestAPIState()
    {
        var dummyAPI = (DummyAPIAccess)API;
        var localUser = dummyAPI.LocalUser;

        foreach (var state in Enum.GetValues<APIState>())
        {
            AddStep($"set API state to {state}", () => dummyAPI.SetState(state));
        }
    }
}
