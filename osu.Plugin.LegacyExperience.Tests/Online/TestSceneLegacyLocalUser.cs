using NUnit.Framework;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;
using osu.Game.Users;
using osu.Plugin.LegacyExperience.Online;

namespace osu.Plugin.LegacyExperience.Tests.Online;

public partial class TestSceneLegacyLocalUser : OsuTestScene
{
    private LegacyLocalUser localUser = null!;

    private readonly Bindable<ScoreBasedUserStatisticsUpdate?> latestUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();

    private readonly Bindable<UserStatistics> currentStatistics = new Bindable<UserStatistics>(new UserStatistics());

    private void updateStatistics(ScoreBasedUserStatisticsUpdate update)
    {
        latestUpdate.Value = update;
        latestUpdate.Value = null;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        currentStatistics.BindValueChanged(stat =>
        {
            if (stat.NewValue is { } newStat)
            {
                var update = new ScoreBasedUserStatisticsUpdate(new ScoreInfo(), stat.OldValue, newStat);
                updateStatistics(update);
            }
        });

        AddStep("create local user", () =>
        {
            localUser?.Expire();

            Add(localUser = new LegacyLocalUser
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                LatestUpdate = { BindTarget = latestUpdate },
            });
        });

        // Allow immediate login/logout for testing purposes.
        ((DummyAPIAccess)API).SessionVerificationMethod = null;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = nameof(MemberwiseClone))]
    private static extern object MemberwiseClone(object obj);

    private UserStatistics cloneStatistics(Action<UserStatistics>? mutator = null)
    {
        // this should be enough in our case
        var clone = (UserStatistics)MemberwiseClone(currentStatistics.Value);
        clone.Variants = currentStatistics.Value.Variants?.ToList();

        mutator?.Invoke(clone);

        return clone;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        AddSliderStep("PP", 0.0, 10000.0, 0, pp => currentStatistics.Value = cloneStatistics(s =>
        {
            s.PP = new decimal(pp);
        }));

        AddSliderStep("Accuracy", 0.0, 100.0, 0, accuracy => currentStatistics.Value = cloneStatistics(s =>
        {
            s.Accuracy = accuracy;
        }));

        AddSliderStep("Global Rank", 0, 100000, 0, rank => currentStatistics.Value = cloneStatistics(s =>
        {
            s.GlobalRank = rank;
        }));

        AddSliderStep("Ranked Score", 0, 1000000000, 0, rankedScore => currentStatistics.Value = cloneStatistics(s =>
        {
            s.RankedScore = rankedScore;
        }));
    }

    [Test]
    public void TestLogout() => AddStep("logout", () => API.Logout());

    [Test]
    public void TestLogin() => AddStep("login", () => API.Login("test", "test"));

    [Test]
    public void TestLoginPeppy() => AddStep("login as peppy", () => API.Login("peppy", "test"));
}
