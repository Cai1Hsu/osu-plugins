using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osuTK;

namespace osu.Plugin.MainMenuPlayer;

public partial class MainMenuPlayerOverlay : CompositeDrawable
{
    [Resolved]
    private GameHost host { get; set; } = null!;

    [Resolved]
    private OsuGame game { get; set; } = null!;

    [Resolved]
    private OsuLogo? osuLogo { get; set; } = null!;

    [Resolved]
    private IdleTracker? idleTracker { get; set; } = null!;

    [Resolved]
    private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

    private TrackMetadataPanel trackMetadataPanel = null!;

    // FIXME: use relative sizing and anchoring for margin, same for other size related values.
    private const float margin = 20f;

    private ButtonSystem? buttonSystem;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            trackMetadataPanel = new TrackMetadataPanel
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Margin = new MarginPadding(margin),
            }
        };

        // technically this can be called from a non-update thread,
        // but we want to skip update if we're not in main menu, so let's schedule it here for simplicity.
        beatmap.BindValueChanged(_ => schedule(updateBeatmapInfo));

        idleTracker?.IsIdle.BindValueChanged(v =>
        {
            if (v.NewValue)
            {
                schedule(() => showPlayer(ActiveState.ActiveByIdle));
            }
        });

        host.Deactivated += () =>
        {
            if (buttonStateAllowsShowPlayer)
            {
                // this is like a temporary show player until we regain focus
                schedule(() => showPlayer(ActiveState.ActiveByMouseMoveOut));
            }
        };

        osuScreenStack = game.ScreenStack;
    }

    private OsuScreenStack osuScreenStack = null!;

    private void newScreenArrives(IScreen _, IScreen screen)
    {
        if (screen is not MainMenu || !screen.IsCurrentScreen())
            return;

        prepare();
    }

    private void prepare()
    {
        // we have to update info first to make components' positions correct
        updateBeatmapInfo();

        hidePlayer();
        finishTransformImmediately();
    }

    private void schedule(Action action)
    {
        // if the menu is inactive, scheduler will not update, resulting actions queuing
        // when returning to main menu, these actions will be performed all at once, making game unresponsive
        if (!mainMenu.IsCurrentScreen())
            return;

        Scheduler.Add(action);
    }

    private MainMenu? mainMenu => Parent as MainMenu;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        if (mainMenu is not null)
            buttonSystem = buttonSystemFieldInfo?.GetValue(mainMenu) as ButtonSystem;

        if (buttonSystem is not null)
        {
            buttonSystem.StateChanged += s =>
            {
                switch (s)
                {
                    case ButtonSystemState.Initial:
                    case ButtonSystemState.Exit:
                        break;

                    default:
                        schedule(restoreMenu);
                        break;
                }
            };
        }

        // Setup initial state if we're already in main menu
        if (mainMenu is not null && mainMenu.IsCurrentScreen())
        {
            mainMenu.InvokeWhenReady(_ => prepare());
        }

        osuScreenStack.ScreenPushed += newScreenArrives;
        osuScreenStack.ScreenExited += newScreenArrives;
    }

    // There's only one private member access in this project,
    // and it's likely it will only be invoked once across the lifetime of the plugin,
    // so use reflection directly here.
    private static readonly FieldInfo? buttonSystemFieldInfo =
        typeof(MainMenu).GetField("Buttons", BindingFlags.NonPublic | BindingFlags.Instance);

    private bool buttonStateAllowsShowPlayer => buttonSystem is null ||
        buttonSystem.State is ButtonSystemState.Initial or ButtonSystemState.Exit;

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

    protected override bool OnMouseMove(MouseMoveEvent e)
    {
        const float edge_threshold = 5f;

        if (buttonStateAllowsShowPlayer)
        {
            var parentBottomRight = Parent.ScreenSpaceDrawQuad.BottomRight;
            var parentTopLeft = Parent.ScreenSpaceDrawQuad.TopLeft;
            var mousePos = e.ScreenSpaceMousePosition;

            // if the mouse move's outside of the screen
            // active track info presentation immediately

            // since the game may be in a scale container,
            // mouse position can be outside of the screen bounds
            if (mousePos.X >= parentBottomRight.X - edge_threshold ||
                mousePos.Y >= parentBottomRight.Y - edge_threshold ||
                // this may seem weird, but it's possible if the game is scaled
                mousePos.X <= parentTopLeft.X + edge_threshold ||
                // Removed the threshold here to prevent conflict with Toolbar
                mousePos.Y <= parentTopLeft.Y)
            {
                showPlayer(ActiveState.ActiveByMouseMoveOut);
            }
            else if (activeState is ActiveState.ActiveByMouseMoveOut)
            {
                // Don't restore if game is inactive
                if (host.IsActive.Value)
                    restoreMenu();
            }
        }

        return base.OnMouseMove(e);
    }

    private void tryRecoverFromInput()
    {
        // Don't restore if game is inactive. Can we receive input when inactive?
        if (host.IsActive.Value &&
            activeState is not ActiveState.Inactive)
        {
            restoreMenu();
        }
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        tryRecoverFromInput();
        return base.OnMouseDown(e);
    }

    protected override bool OnTouchDown(TouchDownEvent e)
    {
        tryRecoverFromInput();
        return base.OnTouchDown(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        // Ignore modifier key presses
        if (!(e.AltPressed ||
            e.ControlPressed ||
            e.SuperPressed ||
            e.SuperPressed))
            tryRecoverFromInput();

        return base.OnKeyDown(e);
    }

    private static string unicodeOrRomaji(string unicode, string romaji) => string.IsNullOrEmpty(unicode) ? romaji : unicode;

    private static bool isBeatmapNonSelected(WorkingBeatmap beatmap)
        => beatmap is DummyWorkingBeatmap;

    private void updateBeatmapInfo()
    {
        var beatmap = this.beatmap.Value;

        var metadata = beatmap.BeatmapInfo.Metadata;

        bool isNonSelected = isBeatmapNonSelected(beatmap);
        var cover = isNonSelected ? null : beatmap.GetBackground();

        void updateInfo()
        {
            if (isNonSelected)
            {
                trackMetadataPanel.Title.Text = "No Beatmap Selected";
                trackMetadataPanel.Artist.Text = "Unknown Artist";
                trackMetadataPanel.Source.Text = string.Empty;
            }
            else
            {
                trackMetadataPanel.Title.Text = unicodeOrRomaji(metadata.TitleUnicode, metadata.Title);
                trackMetadataPanel.Artist.Text = unicodeOrRomaji(metadata.ArtistUnicode, metadata.Artist);
                trackMetadataPanel.Source.Text = metadata.Source;
            }

            if (cover is not null)
            {
                trackMetadataPanel.Cover.CoverSprite.Texture = cover;
                trackMetadataPanel.Cover.FadeIn(transition_duration, Easing.OutQuint);
            }
            else
            {
                trackMetadataPanel.Cover.FadeOut(transition_duration, Easing.OutQuint);
            }
        }

        if (activeState is not ActiveState.Inactive)
        {
            trackMetadataPanel
                .FadeOut(transition_duration, Easing.InQuint)
                .Then()
                .Schedule(updateInfo)
                .FadeIn(transition_duration, Easing.OutQuint);
        }
        else
        {
            updateInfo();
        }
    }

    private ActiveState activeState = ActiveState.Inactive;

    private enum ActiveState
    {
        Inactive = 0,
        // Temporary active state due to mouse moving out of screen
        // Mouse returning to screen will restore menu
        ActiveByMouseMoveOut = 1,
        // Permanent active state due to idle, mouse down or key down to restore
        ActiveByIdle = 2,
    }

    private void showTrackInfo()
    {
        trackMetadataPanel
            .MoveToX(0, transition_duration, Easing.OutCubic)
            .FadeIn(transition_duration, Easing.OutQuint);
    }

    private void hideTrackInfo()
    {
        trackMetadataPanel
            // TODO: this doesn't include margin, so technically it's not fully out of screen
            .MoveToX(-trackMetadataPanel.Width, transition_duration, Easing.OutCubic)
            .FadeOut(transition_duration, Easing.OutQuint);
    }

    private const double transition_duration = 400;

    private void showPlayer(ActiveState state)
    {
        var oldState = activeState;
        activeState = (ActiveState)Math.Max((int)activeState, (int)state);

        if (oldState is not ActiveState.Inactive)
            return;

        osuLogo.FadeOut(transition_duration, Easing.OutQuint);
        game.Toolbar.Hide();
        showTrackInfo();
    }

    private void hidePlayer()
    {
        hideTrackInfo();
        activeState = ActiveState.Inactive;
    }

    private void finishTransformImmediately()
    {
        trackMetadataPanel.FinishTransforms(true);
    }

    private void restoreMenu()
    {
        if (activeState is ActiveState.Inactive)
            return;

        osuLogo.FadeIn(transition_duration, Easing.OutQuint);
        game.Toolbar.Show();
        hidePlayer();
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (isDisposing)
        {
            osuScreenStack.ScreenPushed -= newScreenArrives;
            osuScreenStack.ScreenExited -= newScreenArrives;
        }
    }

    private partial class TrackMetadataPanel : CompositeDrawable
    {
        // TODO: Is this nested too much?
        public partial class TrackCover : CompositeDrawable
        {
            private Sprite coverSprite = null!;

            public Sprite CoverSprite => coverSprite;

            public TrackCover()
            {
                RelativeSizeAxes = Axes.Both;
                FillMode = FillMode.Fit;
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 5f,
                    Child = coverSprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    }
                }.WithEffect(new BlurEffect
                {
                    DrawOriginal = true,
                    Colour = Colour4.Black.Opacity(0.7f),
                });
            }
        }

        public OsuSpriteText Title { get; private set; } = null!;
        public OsuSpriteText Artist { get; private set; } = null!;
        public OsuSpriteText Source { get; private set; } = null!;
        public TrackCover Cover { get; private set; } = null!;

        public TrackMetadataPanel()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;
            RelativePositionAxes = Axes.Y;
            Height = 0.15f;

            InternalChildren = new Drawable[]
            {
                new FillFlowContainer
                {
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Children = new Drawable[]
                    {
                        Cover = new TrackCover(),
                        new FillFlowContainer
                        {
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                Title = new OsuSpriteText()
                                {
                                    Font = OsuFont.GetFont(size: 48, weight: FontWeight.SemiBold),
                                },
                                Artist = new OsuSpriteText()
                                {
                                    Font = OsuFont.GetFont(size: 24),
                                },
                                Source = new OsuSpriteText()
                                {
                                    Font = OsuFont.GetFont(size: 24),
                                },
                            }
                        }
                    }
                }
            };
        }
    }
}
