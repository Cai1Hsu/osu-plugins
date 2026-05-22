using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.Spectate;
using osu.Game.Users;
using osuTK;

namespace osu.Plugin.Patches.RankedPlaySpectator;

/// <summary>
/// This screen serves as a temporary loading screen while we fetch the necessary data to start spectating a ranked play match,
/// such as ensuring beatmap availability and preparing the spectator screen. Once the data is ready, it transitions to the <see cref="RankedPlaySpectatorScreen"/> to start spectating the match.
/// </summary>
public partial class RankedPlaySpectatorLoader : SpectatorScreen, IPreviewTrackOwner
{
    private readonly Room room;
    private readonly APIUser[] users;

    [Resolved]
    private BeatmapLookupCache beatmapLookupCache { get; set; } = null!;

    [Resolved]
    private PreviewTrackManager previewTrackManager { get; set; } = null!;

    [Resolved]
    private BeatmapManager beatmaps { get; set; } = null!;

    [Resolved]
    private BeatmapModelDownloader beatmapDownloader { get; set; } = null!;

    [Cached]
    private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

    private Container beatmapPanelContainer = null!;
    private RoundedButton watchButton = null!;
    private SettingsCheckbox automaticDownload = null!;

    private readonly Dictionary<int, SpectatorState> userStates = new();
    private readonly Dictionary<int, SpectatorGameplayState> userGameplayStates = new();

    private ScheduledDelegate? beatmapFetchCallback;
    private APIBeatmapSet? beatmapSet;

    private readonly Bindable<bool> automaticStart = new Bindable<bool>(true);

    public RankedPlaySpectatorLoader(Room room, APIUser[] users)
        : base(users.Select(u => u.Id).ToArray())
    {
        this.room = room;
        this.users = users;
    }

    [BackgroundDependencyLoader]
    private void load(OsuConfigManager config)
    {
        InternalChild = new Container
        {
            Masking = true,
            CornerRadius = 20,
            AutoSizeAxes = Axes.Both,
            AutoSizeDuration = 500,
            AutoSizeEasing = Easing.OutQuint,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colourProvider.Background5,
                    RelativeSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    Margin = new MarginPadding(20),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Spacing = new Vector2(15),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Ranked Play Spectator Mode",
                            Font = OsuFont.Default.With(size: 30),
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Spacing = new Vector2(15),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Y,
                                    Width = 290,
                                    Direction = FillDirection.Vertical,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Spacing = new Vector2(5),
                                    ChildrenEnumerable = users.Select(u => new UserGridPanel(u)
                                    {
                                        Height = 145,
                                        RelativeSizeAxes = Axes.X,

                                        // Hack to ensure the UserGridPanel is loaded
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    })
                                },
                                new SpriteIcon
                                {
                                    Size = new Vector2(40),
                                    Icon = FontAwesome.Solid.ArrowRight,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                beatmapPanelContainer = new Container
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                            }
                        },
                        automaticDownload = new SettingsCheckbox
                        {
                            LabelText = OnlineSettingsStrings.AutomaticallyDownloadMissingBeatmaps,
                            Current = config.GetBindable<bool>(OsuSetting.AutomaticallyDownloadMissingBeatmaps),
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        new SettingsCheckbox
                        {
                            LabelText = "Automatically start spectating when ready",
                            Current = automaticStart,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                        watchButton = new PurpleRoundedButton
                        {
                            Text = "Start Watching",
                            Width = 250,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Action = scheduleStart,
                            Enabled = { Value = false }
                        }
                    }
                }
            }
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        automaticDownload.Current.BindValueChanged(_ => checkForAutomaticDownload());
    }

    protected override void OnNewPlayingUserState(int userId, SpectatorState spectatorState) => Schedule(() =>
    {
        userStates[userId] = spectatorState;
        checkStates();
    });

    protected override void StartGameplay(int userId, SpectatorGameplayState spectatorGameplayState) => Schedule(() =>
    {
        userGameplayStates[userId] = spectatorGameplayState;
        checkStates();
    });

    protected override void FailGameplay(int userId)
    {
    }

    protected override void QuitGameplay(int userId)
    {
    }

    private void checkStates()
    {
        if (users.Length == 0) return;

        // Ensure we have a state for all users, otherwise they haven't sent state yet
        if (!users.All(u => userStates.ContainsKey(u.Id)))
            return;

        int? beatmapId = userStates.Values.First().BeatmapID;
        bool allSameBeatmap = userStates.Values.All(s => s.BeatmapID == beatmapId);

        if (allSameBeatmap && beatmapId != null)
        {
            showBeatmapPanel(beatmapId.Value);

            bool allGameplayReady = users.All(u => userGameplayStates.ContainsKey(u.Id));
            if (allGameplayReady)
            {
                var firstGameplayState = userGameplayStates.Values.First();
                Beatmap.Value = firstGameplayState.Beatmap;
                Ruleset.Value = firstGameplayState.Ruleset.RulesetInfo;
                watchButton.Enabled.Value = true;

                if (automaticStart.Value)
                    scheduleStart();
            }
            else
            {
                watchButton.Enabled.Value = false;
            }
        }
        else
        {
            clearDisplay();
        }
    }

    private void clearDisplay()
    {
        watchButton.Enabled.Value = false;
        beatmapFetchCallback?.Cancel();
        beatmapPanelContainer.Clear();
        previewTrackManager.StopAnyPlaying(this);
    }

    private ScheduledDelegate? scheduledStart;

    private void scheduleStart()
    {
        scheduledStart?.Cancel();
        scheduledStart = Schedule(() =>
        {
            if (this.IsCurrentScreen())
                start();
            else
                scheduleStart();
        });

        void start()
        {
            var firstGameplayState = userGameplayStates.Values.First();
            Beatmap.Value = firstGameplayState.Beatmap;
            Ruleset.Value = firstGameplayState.Ruleset.RulesetInfo;

            this.Push(new RankedPlaySpectatorScreen(room, users));
        }
    }

    private void showBeatmapPanel(int beatmapId)
    {
        if (beatmapPanelContainer.Children.Count > 0)
            return;

        beatmapLookupCache.GetBeatmapAsync(beatmapId).ContinueWith(t => beatmapFetchCallback = Schedule(() =>
        {
            var beatmap = t.GetResultSafely();

            if (beatmap?.BeatmapSet == null)
                return;

            beatmapSet = beatmap.BeatmapSet;
            beatmapPanelContainer.Child = new BeatmapCardNormal(beatmapSet, allowExpansion: false);
            checkForAutomaticDownload();
        }));
    }

    private void checkForAutomaticDownload()
    {
        if (beatmapSet == null)
            return;

        if (!automaticDownload.Current.Value)
            return;

        if (beatmaps.IsAvailableLocally(new BeatmapSetInfo { OnlineID = beatmapSet.OnlineID }))
            return;

        beatmapDownloader.Download(beatmapSet);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        previewTrackManager.StopAnyPlaying(this);
        return base.OnExiting(e);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        previewTrackManager.StopAnyPlaying(this);
        base.OnSuspending(e);
    }
}
