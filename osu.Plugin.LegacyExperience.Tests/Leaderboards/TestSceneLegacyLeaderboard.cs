using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Framework.Bindables;
using osu.Game.Skinning;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users;
using osu.Game.Screens.Play;
using osu.Game.Tests.Gameplay;
using osu.Game.Rulesets.Osu;
using NUnit.Framework;
using osu.Plugin.LegacyExperience.Leaderboards;
using osu.Game.Configuration;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using System.Diagnostics.CodeAnalysis;

namespace osu.Plugin.LegacyExperience.Tests.Leaderboards;

public partial class TestSceneLegacyLeaderboard : LocalSkinTestScene, IKeyBindingHandler<GlobalAction>
{
    private SkinProvidingContainer skinContainer = null!;
    private readonly BindableInt maxEntries = new BindableInt(6);

    [Cached(typeof(IGameplayLeaderboardProvider))]
    private readonly TestGameplayLeaderboardProvider provider = new TestGameplayLeaderboardProvider();

    [Cached]
    [SuppressMessage("CodeQuality", "IDE0052", Justification = "DI usage")]
    private readonly GameplayState gameplayState = TestGameplayState.Create(new OsuRuleset());

    [Cached(typeof(ILocalUserPlayInfo))]
    private readonly TestLocalUserPlayInfo localUserPlayInfo = new TestLocalUserPlayInfo();

    private Bindable<bool> showLeaderboard = null!;

    private OsuTextFlowContainer displayInfo = null!;

    [BackgroundDependencyLoader]
    private void load(OsuConfigManager osuConfig)
    {
        Add(displayInfo = new OsuTextFlowContainer
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
        });

        Add(skinContainer = new SkinProvidingContainer(new DefaultLegacySkin(this))
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
        });

        showLeaderboard = osuConfig.GetBindable<bool>(OsuSetting.GameplayLeaderboard);

        showLeaderboard.BindValueChanged(_ => updateDisplayInfo());
        localUserPlayInfo.PlayingState.BindValueChanged(_ => updateDisplayInfo());

        var playStates = Enum.GetValues<LocalUserPlayingState>();

        foreach (var state in playStates)
        {
            AddStep($"set {state} state", () => localUserPlayInfo.PlayingState.Value = state);
        }

        AddStep("next play state", () =>
        {
            localUserPlayInfo.PlayingState.Value =
                (LocalUserPlayingState)(((int)localUserPlayInfo.PlayingState.Value + 1) % playStates.Length);
        });

        AddStep("toggle leaderboard", () => showLeaderboard.Value = !showLeaderboard.Value);

        AddToggleStep("use zero-based display order", v =>
        {
            provider.UseZeroBasedDisplayOrder = v;
            updateDisplayInfo();
        });

        AddToggleStep("auto sort", v => provider.AutoSort.Value = v);

        provider.AutoSort.BindValueChanged(_ => updateDisplayInfo(), true);

        void updateDisplayInfo()
        {
            displayInfo.Text = $"Show leaderboard: {showLeaderboard.Value}\n" +
                               $"Local user play state: {localUserPlayInfo.PlayingState.Value}\n" +
                               $"Use zero-based display order: {provider.UseZeroBasedDisplayOrder}\n" +
                               $"Auto sort: {provider.AutoSort.Value}";
        }
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        Box? sizeReference = null;

        AddStep("Create leaderboard", () =>
        {
            skinContainer.Child = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    sizeReference = new Box
                    {
                        Name = "Size reference",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.DarkGray.Opacity(0.3f),
                    },
                    new LegacyLeaderboard
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        MaxEntries = { BindTarget = maxEntries },
                    }
                }
            };
        });

        AddToggleStep("toggle size reference", v => sizeReference?.Alpha = v ? 1 : 0);

        AddSliderStep("Set max entries", 1, 24, 6, v => maxEntries.Value = v);

        AddStep("sort", provider.Sort);
    }

    private void clearScores() => provider.Scores.Clear();

    private void setup_scores_step(int count, Action<GameplayLeaderboardScore>? trackingScoreSetup = null)
    {
        var scores = Enumerable.Range(0, count).Select(i => provider.CreateRandomScore(new APIUser { Username = $"Player {i + 1}" })).ToList();
        var trackingScore = provider.CreateRandomScore(API.LocalUser.Value, true);
        trackingScoreSetup?.Invoke(trackingScore);
        scores.Add(trackingScore);

        AddStep("setup scores", () =>
        {
            clearScores();

            for (int i = 0; i < scores.Count; i++)
            {
                provider.Scores.Add(scores[i]);
            }
        });

        foreach (var score in scores)
            AddSliderStep($"{score.User.Username} score", 0, 5_000_000, score.TotalScore.Value, v => score.TotalScore.Value = v);
    }

    [Test]
    public void TestPlayersPosition()
    {
        setup_scores_step(3, trackingScore =>
        {
            trackingScore.TotalScore.Value = 0;
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    public static extern GameplayLeaderboardScore CreateScore(IUser user, bool tracked, Bindable<long> displayScore);

    public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
    {
        if (!e.Repeat && e.Action is GlobalAction.ToggleInGameLeaderboard)
        {
            showLeaderboard.Value = !showLeaderboard.Value;
            return true;
        }

        return false;
    }

    public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
    {
    }

    private class TestGameplayLeaderboardProvider : IGameplayLeaderboardProvider
    {
        IBindableList<GameplayLeaderboardScore> IGameplayLeaderboardProvider.Scores => Scores;

        public GameplayLeaderboardScore CreateRandomScore(APIUser user, bool isTracked = false)
            => CreateLeaderboardScore(new BindableLong(RNG.Next(0, 5_000_000)), user, isTracked);

        public GameplayLeaderboardScore CreateLeaderboardScore(BindableLong totalScore, APIUser user, bool isTracked = false)
            => CreateScore(user, isTracked, totalScore);

        public bool UseZeroBasedDisplayOrder { get; set; } = true;

        public BindableList<GameplayLeaderboardScore> Scores { get; private set; } = new();

        public readonly BindableBool AutoSort = new BindableBool(true);

        public TestGameplayLeaderboardProvider()
        {
            Scores.BindCollectionChanged((_, __) =>
            {
                var autoSort = AutoSort.Value;
                AutoSort.Value = false; // unbind events to avoid leaking and unintended side effects

                trackingScores.Clear();
                trackingScores.AddRange(Scores.Select(static s => s.TotalScore.GetBoundCopy()));

                AutoSort.Value = autoSort; // rebind events
            }, true);

            AutoSort.BindValueChanged(v =>
            {
                foreach (var score in trackingScores)
                {
                    if (v.NewValue)
                        score.BindValueChanged(_ => Sort(), true);
                    else
                        score.UnbindEvents();
                }
            }, true);
        }

        private List<Bindable<long>> trackingScores = new();

        public void Sort()
        {
            var sorted = Scores
                .OrderByDescending(s => s.TotalScore.Value)
                .ToArray();

            for (int i = 0; i < sorted.Length; i++)
            {
                var score = sorted[i];

                // MultiplayerLeaderboardProvider uses 0-based, but other providers use 1-based.
                score.DisplayOrder.Value = i + (UseZeroBasedDisplayOrder ? 0 : 1);
                score.Position.Value = i + 1;
            }
        }
    }

    private class TestLocalUserPlayInfo : ILocalUserPlayInfo
    {
        public Bindable<LocalUserPlayingState> PlayingState { get; } = new Bindable<LocalUserPlayingState>();

        IBindable<LocalUserPlayingState> ILocalUserPlayInfo.PlayingState => PlayingState;
    }
}
