using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osu.Game.Screens.SelectV2;
using SongSelectV2 = osu.Game.Screens.SelectV2.SongSelect;
using BeatmapCarouselV2 = osu.Game.Screens.SelectV2.BeatmapCarousel;
using LegacyBeatmapCarousel = osu.Plugin.LegacyExperience.SongSelect.BeatmapCarousel;

namespace osu.Plugin.LegacyExperience;

public sealed partial class LegacyExperiencePlugin
{
    private void hookSongSelectScreen(OsuGame game)
    {
        // No-op, the existence of this method is enough to ensure the assembly is loaded
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

            RemoveInternal(carouselParent, currentCarousel, true);

            carouselParent.AddInternal(legacyCarousel);
            ChangeInternalChildDepth(carouselParent, legacyCarousel, previousDepth);

            currentCarousel = legacyCarousel;
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RemoveInternal")]
    private static extern bool RemoveInternal(CompositeDrawable @this, Drawable child, bool disposeImmediately);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ChangeInternalChildDepth")]
    private static extern void ChangeInternalChildDepth(CompositeDrawable @this, Drawable child, float newDepth);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "carousel")]
    private static extern ref BeatmapCarouselV2 get_carousel(SongSelectV2 songSelect);

    private LegacyBeatmapCarousel? createLegacyCarousel(BeatmapCarouselV2 carousel)
    {
        if (carousel is LegacyBeatmapCarousel)
            return null;

        return new LegacyBeatmapCarousel()
        {
            BleedTop = FilterControl.HEIGHT_FROM_SCREEN_TOP + 5,
            BleedBottom = ScreenFooter.HEIGHT + 5,
            RelativeSizeAxes = Axes.Both,
            RequestPresentBeatmap = get_RequestPresentBeatmap(carousel),
            RequestSelection = get_RequestSelection(carousel),
            RequestRecommendedSelection = get_RequestRecommendedSelection(carousel),
            NewItemsPresented = get_NewItemsPresented(carousel),

        };
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_RequestPresentBeatmap")]
    private static extern Action<BeatmapInfo>? get_RequestPresentBeatmap(BeatmapCarouselV2 carousel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_RequestSelection")]
    private static extern Action<GroupedBeatmap> get_RequestSelection(BeatmapCarouselV2 carousel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_RequestRecommendedSelection")]
    private static extern Action<IEnumerable<GroupedBeatmap>> get_RequestRecommendedSelection(BeatmapCarouselV2 carousel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_NewItemsPresented")]
    private static extern Action<IEnumerable<CarouselItem>>? get_NewItemsPresented(Carousel<BeatmapInfo> carousel);
}
