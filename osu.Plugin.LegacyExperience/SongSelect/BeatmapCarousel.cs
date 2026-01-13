using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.SelectV2;
using osu.Game.Skinning;
using BeatmapCarouselV2 = osu.Game.Screens.SelectV2.BeatmapCarousel;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class BeatmapCarousel : BeatmapCarouselV2
{
    [Resolved]
    private ISkinSource? skin { get; set; }

    [Cached]
    private LegacyPanelColors panelColors { get; set; } = LegacyPanelColors.CreateDefault();

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
        var backgroundTexture = skin?.GetTexture("menu-button-background");

        if (backgroundTexture is null)
            return;

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

    protected override Drawable GetDrawableForDisplay(CarouselItem item)
    {
        // TODO: pool and select corresponding LegacyPanel type based on item's model
        switch (item.Model)
        {
            case RankedStatusGroupDefinition:
            case StarDifficultyGroupDefinition:
            case RankDisplayGroupDefinition:
            case GroupDefinition:
                return new LegacyGroupPanel();

            case GroupedBeatmap:
            case GroupedBeatmapSet:
                return new LegacyBeatmapPanel();
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
