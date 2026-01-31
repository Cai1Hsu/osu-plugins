using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Cursor;
using osu.Game.Plugins;
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

    [Cached]
    private LegacyRankSpritePool rankSpritePool { get; set; } = new LegacyRankSpritePool();

    public bool AllowPanelHoverSample => !AbsoluteScrolling &&
        (Scroll.Target == Scroll.Current || Scroll.UserScrolling);

    // SongSelectV2's capacity is 100 foreach panel type.
    // Although V2's panels are more varied, I think 100 is enough.
    private const int pool_capacity = 100;

    private BeatmapCarouselFilterGrouping grouping = null!;

    public BeatmapCarousel()
    {
        AddInternal(starDifficultyPool);
        AddInternal(rankSpritePool);
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

        if (skin is not null)
            skin.SourceChanged += onSkinSourceChanged;

        onSkinSourceChanged();
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

    private Vector2 panelSize = new Vector2(799, 103) * LegacyPanel.TextureScale;

    private static readonly SampleInfo select_expand_sample_info = new SampleInfo("select-expand");
    private static readonly SampleInfo select_difficulty_sample_info = new SampleInfo("select-difficulty");
    private static readonly SampleInfo menu_click_sample_info = new SampleInfo("menuclick");
    private static readonly SampleInfo click_short_confirm_sample_info = new SampleInfo("click-short-confirm");

    private void onSkinSourceChanged()
    {
        var selectExpandSample = skin?.GetSample(select_expand_sample_info);
        var selectDifficultySample = skin?.GetSample(select_difficulty_sample_info);
        var menuClickSample = skin?.GetSample(menu_click_sample_info);
        var randomSelectSample = skin?.GetSample(click_short_confirm_sample_info);

        updateSamples(
            sampleChangeDifficulty: selectDifficultySample,
            sampleChangeSet: selectExpandSample,
            sampleToggleGroup: selectExpandSample,
            spinSample: menuClickSample, // TODO: stable repeatedly plays this sample until you release the button
            randomSelectSample: randomSelectSample,
            sampleKeyboardTraversal: selectDifficultySample
        );

        panelColors.SyncFromSkin(skin);
        updatePanelBackground();
    }

    private static readonly FrozenDictionary<string, FieldInfo> sampleFields = typeof(BeatmapCarouselV2)
        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .Concat(typeof(Carousel<BeatmapInfo>).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        .Where(f => f.FieldType == typeof(Sample))
        .ToFrozenDictionary(f => f.Name, f => f);

    private void updateSamples(ISample? sampleChangeDifficulty, ISample? sampleChangeSet, ISample? sampleToggleGroup, ISample? spinSample, ISample? randomSelectSample, ISample? sampleKeyboardTraversal)
    {
        void updateSample(ISample? sample, [CallerArgumentExpression(nameof(sample))] string filedName = "")
        {
            if (sampleFields.TryGetValue(filedName, out var fieldInfo))
                fieldInfo.SetValue(this, sample as Sample); // explicit cast to avoid invalid assignment
        }

        updateSample(sampleChangeDifficulty);
        updateSample(sampleChangeSet);
        updateSample(sampleToggleGroup);
        updateSample(spinSample);
        updateSample(randomSelectSample);
        updateSample(sampleKeyboardTraversal);
    }

    void updatePanelBackground()
    {
        var backgroundTexture = skin.GetSkinTexture("menu-button-background", textures, "UI");

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

    private InputManager inputManager = null!;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        inputManager = GetContainingInputManager();
    }

    [Resolved]
    private OsuContextMenuContainer? contextMenuContainer { get; set; }

    private LegacyPanel? getPanelWithContextMenu()
    {
        if (contextMenuContainer is null)
            return null;

        var menu = get_menu(contextMenuContainer);

        if (menu is null || menu.State is MenuState.Closed)
            return null;

        var menuTarget = get_menuTarget(contextMenuContainer) as LegacyPanel;

        if (menuTarget?.Item is null)
            return null;

        return menuTarget;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "menu")]
    private static extern ref Menu get_menu(ContextMenuContainer container);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "menuTarget")]
    private static extern ref IHasContextMenu get_menuTarget(ContextMenuContainer container);

    private LegacyPanel? contextMenuActivePanel;

    protected override void Update()
    {
        visibleHalfHeight = (DrawHeight + BleedBottom + BleedTop) / 2;
        frameRatio = Time.Elapsed / (1000 / 60f);

        base.Update();

        var scrollChildren = Scroll.Panels.Children;

        double? hoveredY = null;

        contextMenuActivePanel = getPanelWithContextMenu();

        LegacyPanel? hoverActivePanel = inputManager.HoveredDrawables
                .OfType<LegacyPanel>()
                .FirstOrDefault(p => p.Item is not null && p.Parent == Scroll.Panels)
            // fallback to context menu active panel so that panels keep their position
            // when the user interacts with the context menu
            ?? contextMenuActivePanel;

        hoveredY = hoverActivePanel?.Item!.CarouselYPosition;

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

        Scheduler.Add(() => SchedulerAfterChildren.Add(makePanelsAppearFromScreenRightEdge));
    }

    protected override void UpdateAfterChildren()
    {
        base.UpdateAfterChildren();

        spawnedItems.Clear();
    }

    private void makePanelsAppearFromScreenRightEdge()
    {
        var scrollChildren = Scroll.Panels.Children;

        for (int i = 0; i < scrollChildren.Count; i++)
        {
            var child = scrollChildren[i];

            if (child is not LegacyPanel panel)
                continue;

            panel.X = panelSize.X;
        }
    }

    protected override float GetSpacingBetweenPanels(CarouselItem top, CarouselItem bottom)
    {
        // align with stable's behaviour
        const float default_spacing_with_height = 48 * LegacyExperiencePlugin.StableRatio;

        // in case top's DrawHeight is not yet updated, we use DrawHeight instead of panelSize.Y
        float spacing = default_spacing_with_height - top.DrawHeight;

        if (top.Model is GroupDefinition)
        {
            if (bottom.Model is not GroupDefinition)
                spacing += 10.0f;
        }
        else
        {
            bool isItemConsideredExpanded(CarouselItem item)
            {
                if (item.IsExpanded)
                    return true;

                if (ExpandedBeatmapSet is not null)
                {
                    if (item.Model is GroupedBeatmapSet set && 
                        set.BeatmapSet.Equals(ExpandedBeatmapSet))
                        return true;

                    if (item.Model is GroupedBeatmap beatmap &&
                        (beatmap.Beatmap.BeatmapSet?.Equals(ExpandedBeatmapSet.BeatmapSet) ?? false))
                        return true;
                }

                return CurrentSelectionItem == item;
            }

            bool topHasBeatmap = top.Model is GroupedBeatmap or GroupedBeatmapSet;

            if (topHasBeatmap && (isItemConsideredExpanded(top) || isItemConsideredExpanded(bottom)))
            {
                spacing += 10.0f;
            }
        }

        return spacing;
    }

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

    public double GetUndampedPanelXOffset(LegacyPanel panel)
    {
        Vector2 posInScroll = Scroll.ToLocalSpace(panel.ScreenSpaceDrawQuad.Centre);
        var xPosition = itemXOffsetByYPosition(posInScroll.Y + BleedTop);

        if (panel.IsHovered || ReferenceEquals(contextMenuActivePanel, panel))
            xPosition -= hover_expand_amount_x;

        return xPosition;
    }

    private double itemXOffsetByYPosition(double yPosition)
    {
        // The following model from stable assume panels anchor and origin is Left-sided,
        // But in lazer, we've set panels to TopRight anchor and origin.
        double stable_panel_offset = 640 - panelSize.X;

        return (Math.Min(200.0, Math.Abs((yPosition / visibleHalfHeight - 1) * 75.0)) - stable_panel_offset)
            * LegacyExperiencePlugin.StableRatio;
    }

    private double frameRatio;

    public double GetPanelXOffset(LegacyPanel panel)
    {
        var xPosition = GetUndampedPanelXOffset(panel);

        return dampPanelXOffset(panel.X, xPosition);
    }

    private double dampPanelXOffset(double currentX, double targetX)
    {
        double offsetX = targetX - currentX;
        offsetX *= Math.Pow(0.95, frameRatio);

        return targetX - offsetX;
    }

    protected override float GetPanelXOffset(Drawable panel)
    {
        if (panel is LegacyPanel legacyPanel)
            return (float)GetPanelXOffset(legacyPanel);

        return base.GetPanelXOffset(panel);
    }

    private DrawablePool<LegacyGroupPanel> groupPanelPool = null!;
    private DrawablePool<LegacyBeatmapPanel> beatmapPanelPool = null!;

    protected override Drawable GetDrawableForDisplay(CarouselItem item)
    {
        LegacyPanel setupLegacyPanel(LegacyPanel panel)
        {
            // FIXME: this value is continously damped in stable.
            const double edge_panels_initial_x = 200 * LegacyExperiencePlugin.StableRatio;  // a random value

            double initialX = edge_panels_initial_x;

            if (spawnedItems.TryGetValue(item, out var source))
            {
                // Set position to the group/set panel's position
                Vector2? initialPosition = source.PanelPosition;

                spawnedItems.Remove(item);

                if (initialPosition.HasValue)
                {
                    initialX = initialPosition.Value.X;
                    panel.DrawYPosition = initialPosition.Value.Y;
                    panel.SelectV2DrawYPosition = initialPosition.Value.Y;
                }
            }

            panel.InitialXPosition = initialX;
            return panel;
        }

        switch (item.Model)
        {
            case RankedStatusGroupDefinition:
            case StarDifficultyGroupDefinition:
            case RankDisplayGroupDefinition:
            case GroupDefinition:
                return setupLegacyPanel(groupPanelPool.Get());

            case GroupedBeatmap:
            case GroupedBeatmapSet:
                return setupLegacyPanel(beatmapPanelPool.Get());
        }

        throw new InvalidOperationException($"Unsupported model type: {item.Model?.GetType()}");
    }

    private float visibleHalfHeight;

    private enum SpawnReasonKind
    {
        SetExpanded,
        GroupExpanded,
    }

    protected override bool HandleItemsChanged(NotifyCollectionChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                spawnedItems.Clear();
                break;

            case NotifyCollectionChangedAction.Remove:
                if (args.OldItems is not null)
                {
                    foreach (var oldItem in args.OldItems.OfType<CarouselItem>())
                        spawnedItems.Remove(oldItem);
                }
                break;
        }

        return base.HandleItemsChanged(args);
    }

    private struct SpawnSource
    {
        public Vector2? PanelPosition;
        public CarouselItem? Item;
        public SpawnReasonKind Reason;
    }

    private Dictionary<CarouselItem, SpawnSource> spawnedItems = new();

    protected override void HandleItemActivated(CarouselItem item)
    {
        // TODO: this method contains duplicated code, cleanup later.

        if (ExpandedBeatmapSet is not null)
        {
            // previous beamap set collapsing, set item will be visible
            // calculate a spawn position for the set item based on beatmap panels
            if (grouping.SetItems.TryGetValue(ExpandedBeatmapSet, out var setItems))
            {
                if (setItems.FirstOrDefault(i => i.Model is GroupedBeatmapSet) is CarouselItem setItem)
                {
                    var firstVisibleBeatmapItem = setItems.Where(i => i.IsVisible && i.Model is GroupedBeatmap)
                        // generally, align to top panel looks better in collapsing case
                        .OrderByDescending(i => i.CarouselYPosition)
                        .LastOrDefault();

                    if (firstVisibleBeatmapItem is not null)
                    {
                        var firstVisiblePanel = retrieveActivatedPanel(firstVisibleBeatmapItem);

                        spawnedItems[setItem] = new SpawnSource
                        {
                            Item = item,
                            PanelPosition = firstVisiblePanel is not null
                                ? new Vector2(firstVisiblePanel.X, (float)firstVisiblePanel.DrawYPosition)
                                : calculateSpawnPosition(firstVisibleBeatmapItem),
                            Reason = SpawnReasonKind.SetExpanded
                        };
                    }
                }
            }
        }

        CarouselItem activateItem = item;

        if (item.Model is GroupedBeatmapSet groupedSet &&
            GetSingleBeatmap(groupedSet.BeatmapSet) is BeatmapInfo singleBeatmap &&
            grouping.SetItems.TryGetValue(groupedSet, out var singelSetItems) &&
            singelSetItems.FirstOrDefault(i => i.Model is GroupedBeatmap groupedBeatmap &&
                groupedBeatmap.Beatmap.Equals(singleBeatmap)) is CarouselItem beatmapItem)
            activateItem = beatmapItem;

        base.HandleItemActivated(activateItem);

        LegacyPanel? retrieveActivatedPanel(CarouselItem item) => Scroll.Panels.Children
            .OfType<LegacyPanel>()
            .FirstOrDefault(p => p.Item == item);

        LegacyPanel? panel = null;

        void addSpawnedItemsForExpandedGroup(CarouselItem i, SpawnReasonKind reason)
        {
            spawnedItems[i] = new SpawnSource
            {
                Item = item,
                PanelPosition = panel is not null
                    ? new Vector2(panel.X, (float)panel.DrawYPosition)
                    : calculateSpawnPosition(item),
                Reason = reason
            };
        }

        Vector2 calculateSpawnPosition(CarouselItem item)
        {
            double yPos = toScrollLocalYPosition();
            double xPos = itemXOffsetByYPosition(yPos + BleedTop);

            return new Vector2((float)xPos, (float)yPos);

            double toScrollLocalYPosition()
            {
                double scrollableExtent = -Scroll.Current + Scroll.ScrollableExtent * Scroll.ScrollContent.RelativeAnchorPosition.Y;
                return item.CarouselYPosition + scrollableExtent;
            }
        }

        switch (item.Model)
        {
            case GroupDefinition group:
                if (grouping.GroupItems.TryGetValue(group, out var items))
                {
                    panel = retrieveActivatedPanel(item);

                    foreach (var i in items)
                    {
                        if (i.Model is GroupedBeatmapSet)
                            i.IsVisible &= !i.IsExpanded;

                        addSpawnedItemsForExpandedGroup(i, SpawnReasonKind.GroupExpanded);
                    }
                }
                break;

            case GroupedBeatmapSet groupedBeatmapSet:
                bool isSingleBeatmapSet = GetSingleBeatmap(groupedBeatmapSet.BeatmapSet) is not null;

                // Use the beatmap set as beatmap directly when it has single beatmap.
                item.IsVisible = isSingleBeatmapSet;

                if (grouping.SetItems.TryGetValue(groupedBeatmapSet, out var setItems))
                {
                    panel = retrieveActivatedPanel(item);

                    foreach (var i in setItems)
                    {
                        // beatmap set with only one beatmap is handled as beatmap directly in stable.
                        if (isSingleBeatmapSet)
                        {
                            if (i.Model is GroupedBeatmap)
                                // Simply hide the beatmap item and use the set item as the beatmap item.
                                i.IsVisible = false;
                        }

                        addSpawnedItemsForExpandedGroup(i, SpawnReasonKind.SetExpanded);
                    }
                }
                break;
            default:
                break;
        }
    }

    public static BeatmapInfo? GetSingleBeatmap(BeatmapSetInfo set)
    {
        BeatmapInfo? singleBeatmap = null;

        foreach (var b in set.Beatmaps)
        {
            if (b.Hidden)
                continue;

            if (singleBeatmap is not null)
                return null;

            singleBeatmap = b;
        }

        return singleBeatmap;
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
