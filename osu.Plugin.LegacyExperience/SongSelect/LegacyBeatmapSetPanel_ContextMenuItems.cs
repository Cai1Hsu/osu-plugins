// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is copied from osu!lazer's PanelBeatmapSet.cs
// Original file: https://github.com/ppy/osu/blob/1add946db486c866cc214c5eb3d728f308aad637/osu.Game/Screens/SelectV2/PanelBeatmapSet.cs

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Plugin.LegacyExperience.SongSelect;

partial class LegacyBeatmapSetPanel
{
    [Resolved]
    private BeatmapSetOverlay? beatmapOverlay { get; set; }

    [Resolved]
    private OsuGame? game { get; set; }

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    [Resolved]
    private IBindable<RulesetInfo> ruleset { get; set; } = null!;

    [Resolved]
    private RealmAccess realm { get; set; } = null!;

    [Resolved]
    private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

    protected MenuItem[] createMenuItemsForBeatmapSet(BeatmapSetInfo beatmapSet)
    {
        List<MenuItem> items = new List<MenuItem>();

        if (Expanded.Value)
        {
            if (songSelect is SoloSongSelect soloSongSelect)
            {
                // Assume the current set has one of its beatmaps selected since it is expanded.
                items.Add(new OsuMenuItem(ButtonSystemStrings.Edit.ToSentence(), MenuItemType.Standard, () => soloSongSelect.Edit(soloSongSelect.Beatmap.Value.BeatmapInfo))
                {
                    Icon = FontAwesome.Solid.PencilAlt
                });
                items.Add(new OsuMenuItemSpacer());
            }
        }
        else
        {
            items.Add(new OsuMenuItem(WebCommonStrings.ButtonsExpand.ToSentence(), MenuItemType.Highlighted, () => TriggerClick()));
            items.Add(new OsuMenuItemSpacer());
        }

        if (beatmapSet.OnlineID > 0)
        {
            items.Add(new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmapSet(beatmapSet.OnlineID)));

            if (beatmapSet.GetOnlineURL(api, ruleset.Value) is string url)
                items.Add(new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard(url)));

            items.Add(new OsuMenuItemSpacer());
        }

        var collectionItems = realm.Realm.All<BeatmapCollection>()
                                   .OrderBy(c => c.Name)
                                   .AsEnumerable()
                                   .Select(createCollectionMenuItem)
                                   .ToList();

        if (manageCollectionsDialog != null)
            collectionItems.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, manageCollectionsDialog.Show));

        items.Add(new OsuMenuItem(CommonStrings.Collections) { Items = collectionItems });

        if (beatmapSet.Beatmaps.Any(b => b.Hidden))
            items.Add(new OsuMenuItem(SongSelectStrings.RestoreAllHidden, MenuItemType.Standard, () => songSelect?.RestoreAllHidden(beatmapSet)));

        items.Add(new OsuMenuItem(CommonStrings.DeleteWithConfirmation, MenuItemType.Destructive, () => songSelect?.Delete(beatmapSet)));
        return items.ToArray();
    }

    private MenuItem createCollectionMenuItem(BeatmapCollection collection)
    {
        var groupedBeatmapSet = Item?.Model as GroupedBeatmapSet;

        Debug.Assert(groupedBeatmapSet is not null);

        var beatmapSet = groupedBeatmapSet.BeatmapSet;

        TernaryState state;

        int countExisting = beatmapSet.Beatmaps.Count(b => collection.BeatmapMD5Hashes.Contains(b.MD5Hash));

        if (countExisting == beatmapSet.Beatmaps.Count)
            state = TernaryState.True;
        else if (countExisting > 0)
            state = TernaryState.Indeterminate;
        else
            state = TernaryState.False;

        var liveCollection = collection.ToLive(realm);

        return new TernaryStateToggleMenuItem(collection.Name, MenuItemType.Standard, s =>
        {
            Task.Run(() => liveCollection.PerformWrite(c =>
            {
                foreach (var b in beatmapSet.Beatmaps)
                {
                    switch (s)
                    {
                        case TernaryState.True:
                            if (c.BeatmapMD5Hashes.Contains(b.MD5Hash))
                                continue;

                            c.BeatmapMD5Hashes.Add(b.MD5Hash);
                            break;

                        case TernaryState.False:
                            c.BeatmapMD5Hashes.Remove(b.MD5Hash);
                            break;
                    }
                }
            }));
        })
        {
            State = { Value = state }
        };
    }
}