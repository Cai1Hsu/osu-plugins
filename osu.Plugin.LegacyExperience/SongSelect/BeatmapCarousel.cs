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

            panel.X = GetUndampedPanelXOffset(panel) + panelSize.X;
        }
    }

    private Scheduler delayedScheduler = new Scheduler();

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
        delayedScheduler.Add(makePanelsAppearFromScreenRightEdge);

        return items;
    }

    public float GetUndampedPanelXOffset(LegacyPanel panel)
    {
        var xPosition = base.GetPanelXOffset(panel);

        if (panel.IsHovered)
            xPosition -= hover_expand_amount_x;

        return xPosition;
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
        // TODO: reset state when reusing from pool
        switch (item.Model)
        {
            case RankedStatusGroupDefinition:
            case StarDifficultyGroupDefinition:
            case RankDisplayGroupDefinition:
            case GroupDefinition:
                return groupPanelPool.Get(setupPanel);

            case GroupedBeatmap:
            case GroupedBeatmapSet:
                return beatmapPanelPool.Get(setupPanel);
        }

        throw new InvalidOperationException($"Unsupported model type: {item.Model?.GetType()}");
    }

    // Set a initial x position for newly created panels so that panels are visible when rapidly scrolling.
    // FIXME: this broke "makePanelsAppearFromScreenRightEdge"
    private void setupPanel(LegacyPanel p)
    {
        // TODO: this is a approximation, we assume all new panels appears from the edge of the screen.
        // but this is not always true, e.g. when expand a beatmap set, new beatmaps appear from where the set was.
        // This tries to restore stable's behaviour, but there're still some panels popping in certain cases.
        p.X = PanelOffsetXAtScreenHorizontalEdge();
    }

    private float visibleHalfHeight;

    private float PanelOffsetXAtScreenHorizontalEdge()
    {
        return offsetX(1, visibleHalfHeight);
    }

    // Carousel<T>'s internal positioning model
    private static float offsetX(float dist, float halfHeight)
    {
        // The radius of the circle the carousel moves on.
        const float circle_radius = 3;
        float discriminant = MathF.Max(0, circle_radius * circle_radius - dist * dist);
        return (circle_radius - MathF.Sqrt(discriminant)) * halfHeight;
    }

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
