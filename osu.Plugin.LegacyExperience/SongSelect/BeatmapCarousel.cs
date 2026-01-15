using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Textures;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.SelectV2;
using osu.Game.Skinning;
using osuTK;
using BeatmapCarouselV2 = osu.Game.Screens.SelectV2.BeatmapCarousel;
using PanelV2 = osu.Game.Screens.SelectV2.Panel;

namespace osu.Plugin.LegacyExperience.SongSelect;

[Cached]
public partial class BeatmapCarousel : BeatmapCarouselV2
{
    [Resolved]
    private ISkinSource? skin { get; set; }

    [Resolved]
    private TextureStore? textures { get; set; }

    [Cached]
    private LegacyPanelColors panelColors { get; set; } = LegacyPanelColors.CreateDefault();

    [Cached]
    private DrawablePool<StarDifficultyDisplay> starDifficultyPool { get; set; } = new DrawablePool<StarDifficultyDisplay>(20);

    // SongSelectV2's capacity is 100 foreach panel type.
    // Although V2's panels are more varied, I think 100 is enough.
    private const int pool_capacity = 100;

    private BeatmapCarouselFilterGrouping grouping = null!;

    public BeatmapCarousel()
    {
        AddInternal(starDifficultyPool);
    }

    private static readonly FieldInfo groupingField = typeof(BeatmapCarouselV2)
        .GetField("grouping", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [BackgroundDependencyLoader]
    private void load()
    {
        disposePanelV2Pools();

        AddInternal(groupPanelPool = new DrawablePool<LegacyGroupPanel>(pool_capacity));
        AddInternal(beatmapPanelPool = new DrawablePool<LegacyBeatmapPanel>(pool_capacity));

        grouping = (BeatmapCarouselFilterGrouping)groupingField.GetValue(this)!;

        Debug.Assert(grouping is not null);
    }

    // These pools are used for SongSelectV2 panels, we don't need them anymore.
    private static readonly ImmutableArray<FieldInfo> poolFields = typeof(BeatmapCarouselV2)
        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(f => typeof(IDrawablePool).IsAssignableFrom(f.FieldType) &&
            f.FieldType.IsGenericType &&
            f.FieldType.GetGenericTypeDefinition() == typeof(DrawablePool<>) &&
            f.FieldType.GetGenericArguments()[0].IsSubclassOf(typeof(PanelV2)))
        .ToImmutableArray();

    private void disposePanelV2Pools()
    {
        foreach (var pool in poolFields)
            dispose(pool);

        void dispose(FieldInfo? fieldInfo)
        {
            Debug.Assert(fieldInfo is not null);

            if (fieldInfo.GetValue(this) is not Drawable pool)
                return;

            RemoveInternal(pool, true); // dispose immediately to release resources
            fieldInfo.SetValue(this, null); // remove reference
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        if (skin is not null)
            skin.SourceChanged += onSkinSourceChanged;

        onSkinSourceChanged();
    }

    private Vector2 panelSize = new Vector2(799, 103) * LegacyPanel.TextureScale;

    private void onSkinSourceChanged()
    {
        panelColors.SyncFromSkin(skin);
        updatePanelBackground();
    }

    void updatePanelBackground()
    {
        var backgroundTexture = LegacyPanel.GetBackgroundTexture(skin, textures);

        // texture should be non-null since we've packed a default one.
        Debug.Assert(backgroundTexture is not null);

        panelSize = backgroundTexture.DisplaySize * LegacyPanel.TextureScale;

        var filterAfterItemsChanged = get_filter_after_items_changed(this);
        filterAfterItemsChanged.Invalidate();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "selectionValid")]
    private static extern ref Cached get_selection_valid(Carousel<BeatmapInfo> carousel);

    // FIXME: POC stage temporarily uses unsafe accessor
    // use reflection as some deploy platforms do not support UnsafeAccessor
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "filterAfterItemsChanged")]
    private static extern ref Cached get_filter_after_items_changed(Carousel<BeatmapInfo> carousel);

    private const float hover_expand_amount_y = 10;

    private const float hover_expand_amount_x = 30;

    protected override void Update()
    {
        visibleHalfHeight = (DrawHeight + BleedBottom + BleedTop) / 2;
        frameRatio = Time.Elapsed / (1000 / 60f);

        base.Update();

        var scrollChildren = Scroll.Panels.Children;

        double? hoveredY = null;

        for (int i = 0; i < scrollChildren.Count; i++)
        {
            var child = scrollChildren[i];
            if (child is not LegacyPanel panel)
                continue;

            if (panel.IsHovered && panel.Item is CarouselItem item)
                hoveredY = item.CarouselYPosition;
        }

        // expand the hovered panel and push others away
        double dampingFactor = Math.Pow(0.875, frameRatio);

        // bypass Carousel's Y position damping
        for (int i = 0; i < scrollChildren.Count; i++)
        {
            var child = scrollChildren[i];

            if (child is not LegacyPanel panel)
                continue;

            if (panel.Item is not CarouselItem item)
                continue;

            double targetY = item.CarouselYPosition;

            if (hoveredY.HasValue && !panel.IsHovered)
            {
                if (targetY > hoveredY.Value)
                    targetY += hover_expand_amount_y;
                else if (targetY < hoveredY.Value)
                    targetY -= hover_expand_amount_y;
            }

            double currentY = panel.SelectV2DrawYPosition == item.CarouselYPosition
                ? panel.SelectV2DrawYPosition // newly added panels
                : panel.DrawYPosition; // our managed Y position, used to bypass Carousel's damping

            double offsetY = targetY - currentY;
            offsetY *= dampingFactor;

            panel.SelectV2DrawYPosition = targetY - offsetY;
            panel.DrawYPosition = panel.SelectV2DrawYPosition;
        }
    }

    protected override void HandleFilterCompleted()
    {
        base.HandleFilterCompleted();

        Scheduler.Add(() => delayedScheduler.Add(makePanelsAppearFromScreenRightEdge));
    }

    protected override void UpdateAfterChildren()
    {
        base.UpdateAfterChildren();

        delayedScheduler.Update();
    }

    private void makePanelsAppearFromScreenRightEdge()
    {
        var scrollChildren = Scroll.Panels.Children;

        for (int i = 0; i < scrollChildren.Count; i++)
        {
            var child = scrollChildren[i];

            if (child is not LegacyPanel panel)
                continue;

            panel.X += panelSize.X;
        }
    }

    private readonly Scheduler delayedScheduler = new Scheduler();

    protected override float GetSpacingBetweenPanels(CarouselItem previousVisible, CarouselItem bottom)
        => 0; // seems good enough, maybe reference for stable later

    protected override async Task<IEnumerable<CarouselItem>> FilterAsync(bool clearExistingPanels = false)
    {
        var items = await base.FilterAsync(clearExistingPanels);

        foreach (var item in items)
            item.DrawHeight = panelSize.Y;

        // trigger recalculation of items' Y positions
        // x position calculation requires proper Y positions
        get_selection_valid(this).Invalidate();

        return items;
    }

    public float GetUndampedPanelXOffset(LegacyPanel panel)
    {
        Vector2 posInScroll = Scroll.ToLocalSpace(panel.ScreenSpaceDrawQuad.Centre);
        var xPosition = itemXOffsetByYPosition(posInScroll.Y + BleedTop);

        if (panel.IsHovered)
            xPosition -= hover_expand_amount_x;

        return (float)xPosition;
    }

    private double itemXOffsetByYPosition(double yPosition)
    {
        // The following model from stable assume panels anchor and origin is Left-sided,
        // But in lazer, we've set panels to TopRight anchor and origin.
        double stable_panel_offset = (640 - panelSize.X) * LegacyExperiencePlugin.StableRatio;

        return Math.Min(200.0, Math.Abs((1f - yPosition / visibleHalfHeight) * 75.0)) - stable_panel_offset;
    }

    private double frameRatio;

    public float GetPanelXOffset(LegacyPanel panel)
    {
        var xPosition = GetUndampedPanelXOffset(panel);

        return dampPanelXOffset(panel.X, xPosition);
    }

    private float dampPanelXOffset(float currentX, float targetX)
    {
        float offsetX = targetX - currentX;
        offsetX *= (float)Math.Pow(0.95, frameRatio);

        return targetX - offsetX;
    }

    protected override float GetPanelXOffset(Drawable panel)
    {
        if (panel is LegacyPanel legacyPanel)
            return GetPanelXOffset(legacyPanel);

        return base.GetPanelXOffset(panel);
    }

    private DrawablePool<LegacyGroupPanel> groupPanelPool = null!;
    private DrawablePool<LegacyBeatmapPanel> beatmapPanelPool = null!;

    protected override Drawable GetDrawableForDisplay(CarouselItem item)
    {
        LegacyPanel setup(LegacyPanel p)
        {
            double? initialYPosition = null;

            if (item.Model is GroupedBeatmap grouped)
            {
                // if the beatmap is from a set or a group, make it appear from the group/set item.
                initialYPosition = grouped.Group is null ? tryGetBeatmapSetItemYPosition()
                    : tryGetGroupItemYPosition();
            }

            float initialXPosition = (float)itemXOffsetByYPosition(initialYPosition ?? item.CarouselYPosition);

            if (initialYPosition.HasValue)
                p.SelectV2DrawYPosition = p.DrawYPosition = initialYPosition.Value;

            // FIXME: this generally looks correct, but there's two issue caused by using delayedScheduler:
            // 1. delayedScheduler runs after ALL chindren, including the scroll container,
            //    so draw info may delay a frame, causing some panels invisible when rapidly scrolling.
            // 2. HandleFilterCompleted requires to reset positions again, however, the follwing line
            //    runs after that, causing `HandleFilterCompleted`'s position reset ineffective.
            delayedScheduler.Add(() => p.X = initialXPosition);

            return p;

            double? tryGetBeatmapSetItemYPosition()
            {
                if (ExpandedBeatmapSet is not null &&
                    ExpandedBeatmapSet.BeatmapSet.Equals(grouped.Beatmap.BeatmapSet) &&
                    grouping.SetItems.TryGetValue(ExpandedBeatmapSet, out var items))
                {
                    return items.FirstOrDefault(i => i.Model is GroupedBeatmapSet)?.CarouselYPosition;
                }

                return null;
            }

            double? tryGetGroupItemYPosition()
            {
                if (grouped.Group is not null &&
                    ExpandedGroup is not null &&
                    grouped.Group == ExpandedGroup &&
                    grouping.GroupItems.TryGetValue(ExpandedGroup, out var items))
                {
                    return items.FirstOrDefault(i => i.Model is GroupDefinition)?.CarouselYPosition;
                }

                return null;
            }
        }

        // TODO: reset state when reusing from pool
        switch (item.Model)
        {
            case RankedStatusGroupDefinition:
            case StarDifficultyGroupDefinition:
            case RankDisplayGroupDefinition:
            case GroupDefinition:
                return setup(groupPanelPool.Get());

            case GroupedBeatmap:
            case GroupedBeatmapSet:
                return setup(beatmapPanelPool.Get());
        }

        throw new InvalidOperationException($"Unsupported model type: {item.Model?.GetType()}");
    }

    private float visibleHalfHeight;

    protected override void HandleItemActivated(CarouselItem item)
    {
        base.HandleItemActivated(item);

        switch (item.Model)
        {
            case GroupDefinition group:
                if (grouping.GroupItems.TryGetValue(group, out var items))
                {
                    foreach (var i in items.Where(i => i.Model is GroupedBeatmapSet))
                        i.IsVisible &= !i.IsExpanded;
                }
                break;


            case GroupedBeatmapSet:
                item.IsVisible = false;
                break;
            default:
                break;
        }
    }

    public bool IsBeatmapPanelFromExpandedSet(LegacyBeatmapPanel panel)
    {
        if (panel.Item is not CarouselItem item)
            return false;

        if (item.Model is not GroupedBeatmap beatmap)
            return false;

        // When not grouped, only beatmaps from expanded set are displayed.
        if (beatmap.Group is null)
            return true;

        if (ExpandedBeatmapSet is null)
            return false;

        return ExpandedBeatmapSet.BeatmapSet.Equals(beatmap.Beatmap.BeatmapSet);
    }

    protected override void HandleItemSelected(object? model)
    {
        // align with stable's behaviour:
        // Hide beatmap set item when one of its beatmaps is selected(set expanded).
        // TODO: there's still one difference: if a beatmap set has only one beatmap,
        // stable treats the single beatmap directly as a set item, thus no hiding occurs.
        bool handleBeatmapSetExpansion = grouping.BeatmapSetsGroupedTogether && model is GroupedBeatmap;

        // restore visibility of previous 
        if (handleBeatmapSetExpansion && ExpandedBeatmapSet is not null)
        {
            bool isInSameGroup = (model as GroupedBeatmap)?.Group == ExpandedBeatmapSet.Group;
            setVisibilityOfSetItem(ExpandedBeatmapSet, i => i.IsVisible |= i.IsExpanded && isInSameGroup);
        }

        base.HandleItemSelected(model);

        // hide newly expanded set item
        if (handleBeatmapSetExpansion && ExpandedBeatmapSet is not null)
            setVisibilityOfSetItem(ExpandedBeatmapSet, static i => i.IsVisible = false);
    }

    private void setVisibilityOfSetItem(GroupedBeatmapSet set, Action<CarouselItem> action)
    {
        if (grouping.SetItems.TryGetValue(set, out var items))
        {
            foreach (var item in items)
            {
                if (item.Model is GroupedBeatmapSet)
                    action(item);
            }
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= onSkinSourceChanged;
    }
}
