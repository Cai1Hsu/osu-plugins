using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.SelectV2;
using SongSelectV2 = osu.Game.Screens.SelectV2.SongSelect;
using SoloSongSelectV2 = osu.Game.Screens.SelectV2.SoloSongSelect;
using BeatmapCarouselV2 = osu.Game.Screens.SelectV2.BeatmapCarousel;
using LegacyBeatmapCarousel = osu.Plugin.LegacyExperience.SongSelect.BeatmapCarousel;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Plugin.LegacyExperience.Mods;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Footer;
using osuTK;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Utils;

namespace osu.Plugin.LegacyExperience;

public sealed partial class LegacyExperiencePlugin
{
    // it's probably fine to do this since each time we load a plugin we create a new instance of it.
    private OsuGame game = null!;

    private void hookSongSelectScreen(OsuGame game)
    {
        this.game = game;
        var screenStack = game.ScreenStack;

        screenStack.ScreenPushed += screenStack_ScreenSwitched;
        screenStack.ScreenExited += screenStack_ScreenSwitched;
    }

    private void screenStack_ScreenSwitched(IScreen oldScreen, IScreen newScreen)
    {
        // PlaylistSongSelectV2 is nested inside Playlists OnlinePlayScreen, hook sub-screen stacks to catch it
        // TODO: investigate whether this change should be applied to other screen trackings
        if (oldScreen is IHasSubScreenStack oldHasSubScreenStack)
        {
            var subScreenStack = oldHasSubScreenStack.SubScreenStack;
            subScreenStack.ScreenPushed -= screenStack_ScreenSwitched;
            subScreenStack.ScreenExited -= screenStack_ScreenSwitched;
        }

        if (newScreen is IHasSubScreenStack hasSubScreenStack)
        {
            var subScreenStack = hasSubScreenStack.SubScreenStack;

            // unsubscribe first to avoid multiple subscriptions
            subScreenStack.ScreenPushed -= screenStack_ScreenSwitched;
            subScreenStack.ScreenExited -= screenStack_ScreenSwitched;

            subScreenStack.ScreenPushed += screenStack_ScreenSwitched;
            subScreenStack.ScreenExited += screenStack_ScreenSwitched;
        }

        if (newScreen is not SongSelectV2 songSelect)
            return;

        songSelect.InvokeWhenReady(d =>
        {
            var songSelect = (SongSelectV2)d;

            ref var currentCarousel = ref get_carousel(songSelect);
            var carouselParent = currentCarousel.Parent;

            if (carouselParent is null)
                return;

            var legacyCarousel = createLegacyCarousel(currentCarousel);

            if (legacyCarousel is null)
                return;

            var previousDepth = currentCarousel.Depth;

            carouselParent.RemoveInternal(currentCarousel, true);

            carouselParent.AddInternal(legacyCarousel);
            carouselParent.ChangeInternalChildDepth(legacyCarousel, previousDepth);

            currentCarousel = legacyCarousel;

            // Available mods are restricted determined by the type of song select screen,
            // extra actions needed for playlist song select as it uses the same carousel.
            // currently only allow access to legacy mod selection in solo song select.
            if (songSelect is SoloSongSelectV2)
            {
                // a stub used to keep track of the lifetime of the mod select screen, 
                // also used as the entry point for opening the mod select screen.
                addLegacyModSelectStub();
            }
        });
    }

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "carousel")]
    private static extern ref BeatmapCarouselV2 get_carousel(SongSelectV2 songSelect);

    private LegacyBeatmapCarousel? createLegacyCarousel(BeatmapCarouselV2 carousel)
    {
        if (carousel.GetType() != typeof(BeatmapCarouselV2))
            return null;

        return new LegacyBeatmapCarousel()
        {
            BleedTop = carousel.BleedTop,
            BleedBottom = carousel.BleedBottom,
            RelativeSizeAxes = carousel.RelativeSizeAxes,
            RequestPresentBeatmap = get_RequestPresentBeatmap(carousel),
            RequestSelection = get_RequestSelection(carousel),
            RequestRecommendedSelection = get_RequestRecommendedSelection(carousel),
            NewItemsPresented = get_NewItemsPresented(carousel),
        };
    }

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_RequestPresentBeatmap")]
    private static extern Action<BeatmapInfo>? get_RequestPresentBeatmap(BeatmapCarouselV2 carousel);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_RequestSelection")]
    private static extern Action<GroupedBeatmap> get_RequestSelection(BeatmapCarouselV2 carousel);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_RequestRecommendedSelection")]
    private static extern Action<IEnumerable<GroupedBeatmap>> get_RequestRecommendedSelection(BeatmapCarouselV2 carousel);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_NewItemsPresented")]
    private static extern Action<IEnumerable<CarouselItem>>? get_NewItemsPresented(Carousel<BeatmapInfo> carousel);

    private void addLegacyModSelectStub()
    {
        ScreenFooter? screenFooter = game.Dependencies.Get<ScreenFooter>();

        if (screenFooter is null)
            return;

        var footerContent = get_FooterContent(screenFooter);
        footerContent.Add(new ModSelectStub());
    }

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "buttonsFlow")]
    private extern static FillFlowContainer<ScreenFooterButton> get_FooterContent(ScreenFooter footer);

    private partial class UserModSelection : LegacyModSelection, IKeyBindingHandler<GlobalAction>
    {
        public override bool HandleNonPositionalInput => true;
        public override bool RequestsFocus => true;

        // block input to underlying carousel and other elements.
        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e) => true;

        public Action? CloseAction { get; init; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var focusManager = GetContainingFocusManager();
            focusManager.ChangeFocus(null);
            focusManager.ChangeFocus(this);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!e.Repeat && e.Action is GlobalAction.Back)
            {
                Close();
                return true;
            }
            return false;
        }

        public override void Close()
        {
            base.Close();
            CloseAction?.Invoke();
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }

    private partial class ModSelectStub : ScreenFooterButton
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        private LegacyModSelection? modSelection;

        public ModSelectStub()
        {
            // make it invisible but still present and receive input
            Size = new Vector2(0);
            AlwaysPresent = true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (modSelection is not null)
            {
                // avoid capturing this in Dispose
                var modSelect = modSelection;
                game.Scheduler.Add(() =>
                {
                    if (modSelect.IsAlive)
                        modSelect.Close();
                });
            }
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            // FIXME: event not received when alt is pressed
            if (!e.Repeat && !e.AltPressed && e.Action is GlobalAction.ToggleModSelection)
            {
                if (modSelection is null)
                {
                    game.Add(modSelection = new UserModSelection()
                    {
                        CloseAction = () => modSelection = null,
                    });
                    modSelection.Show();
                }
                else
                {
                    modSelection.Close();
                    modSelection = null;
                }
                return true;
            }

            return false;
        }
    }
}
