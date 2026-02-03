using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Screens.SelectV2;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyBeatmapSetPanel : LegacyPanelHasBeatmap
{
    protected override PanelDisplayPolicy CreateDisplayPolicy(object model)
    {
        var groupedBeatmapSet = (GroupedBeatmapSet)model;
        var beatmapSetInfo = groupedBeatmapSet.BeatmapSet;
        var metadata = beatmapSetInfo.Metadata;

        return new PanelDisplayPolicy(
            new RomanisableString(metadata.TitleUnicode, metadata.Title),
            new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
            beatmapSetInfo.Beatmaps.MinBy(b => b.OnlineID)
        );
    }

    public override MenuItem[]? ContextMenuItems
    {
        get
        {
            if (Item?.Model is GroupedBeatmapSet groupedBeatmapSet)
                return createMenuItemsForBeatmapSet(groupedBeatmapSet.BeatmapSet);

            return Array.Empty<MenuItem>();
        }
    }
}
