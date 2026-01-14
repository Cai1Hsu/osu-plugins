using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.SelectV2;
using osu.Game.Skinning;
using BeatmapCarouselV2 = osu.Game.Screens.SelectV2.BeatmapCarousel;
using PanelV2 = osu.Game.Screens.SelectV2.Panel;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class BeatmapCarousel : BeatmapCarouselV2
{
    [Resolved]
    private ISkinSource? skin { get; set; }

    [Resolved]
    private TextureStore? textures { get; set; }

    [Cached]
    private LegacyPanelColors panelColors { get; set; } = LegacyPanelColors.CreateDefault();

    // SongSelectV2's capacity is 100 foreach panel type.
    // Although V2's panels are more varied, I think 100 is enough.
    private const int pool_capacity = 100;

    [BackgroundDependencyLoader]
    private void load()
    {
        disposePanelV2Pools();

        AddInternal(groupPanelPool = new DrawablePool<LegacyGroupPanel>(pool_capacity));
        AddInternal(beatmapPanelPool = new DrawablePool<LegacyBeatmapPanel>(pool_capacity));
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

    private float panelHeight = 103 * 0.6f;

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

        panelHeight = backgroundTexture.DisplayHeight * LegacyPanel.TextureScale;

        var filterAfterItemsChanged = get_filter_after_items_changed(this);
        filterAfterItemsChanged.Invalidate();
    }

    // FIXME: POC stage temporarily uses unsafe accessor
    // use reflection as some deploy platforms do not support UnsafeAccessor
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "filterAfterItemsChanged")]
    private static extern ref Cached get_filter_after_items_changed(Carousel<BeatmapInfo> carousel);

    private const float hover_expand_amount_y = 10;

    private const float hover_expand_amount_x = 50;

    protected override void Update()
    {
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
        double frameRatio = Time.Elapsed / (1000 / 60f);
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

            double currentY = panel.OsuDrawYPosition == item.CarouselYPosition
                ? panel.OsuDrawYPosition // newly added panels
                : panel.DrawYPosition; // our managed Y position, used to bypass Carousel's damping

            double offsetY = targetY - currentY;
            offsetY *= dampingFactor;

            panel.OsuDrawYPosition = targetY - offsetY;
            panel.DrawYPosition = panel.OsuDrawYPosition;
        }
    }

    protected override float GetSpacingBetweenPanels(CarouselItem previousVisible, CarouselItem bottom)
        => 0; // seems good enough, maybe reference for stable later

    protected override async Task<IEnumerable<CarouselItem>> FilterAsync(bool clearExistingPanels = false)
    {
        var items = await base.FilterAsync(clearExistingPanels);

        foreach (var item in items)
            item.DrawHeight = panelHeight;

        return items;
    }

    protected override float GetPanelXOffset(Drawable panel)
    {
        var xPosition = base.GetPanelXOffset(panel);

        if (panel is LegacyPanel legacyPanel)
        {
            if (panel.IsHovered)
                xPosition -= hover_expand_amount_x;

            float currentX = legacyPanel.X;
            double frameRatio = Time.Elapsed / (1000 / 60f);
            float offsetX = xPosition - currentX;
            offsetX *= (float)Math.Pow(0.95, frameRatio);

            xPosition -= offsetX;
        }

        return xPosition;
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
                return groupPanelPool.Get();

            case GroupedBeatmap:
            case GroupedBeatmapSet:
                return beatmapPanelPool.Get();
        }

        throw new InvalidOperationException($"Unsupported model type: {item.Model?.GetType()}");
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= onSkinSourceChanged;
    }
}
