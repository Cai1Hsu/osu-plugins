// This file is adapted from osu!lazer's PanelLocalRankDisplay to work in the Legacy Experience plugin.
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Original file: https://github.com/ppy/osu/blob/952fd0d493eb3cd9994ea8ff4e27b44e82c1f287/osu.Game/Screens/SelectV2/PanelLocalRankDisplay.cs

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using Realms;
using static osu.Plugin.LegacyExperience.SongSelect.LegacyRankSpritePool;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyLocalRankDisplay : CompositeDrawable
{
    private BeatmapInfo? beatmap;

    public BeatmapInfo? Beatmap
    {
        get => beatmap;
        set
        {
            beatmap = value;

            if (IsLoaded)
                updateSubscription();
        }
    }

    [Resolved]
    private IBindable<RulesetInfo> ruleset { get; set; } = null!;

    [Resolved]
    private RealmAccess realm { get; set; } = null!;

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    [Resolved]
    private LegacyRankSpritePool? rankSpritePool { get; set; }

    private IDisposable? scoreSubscription;

    private LegacyRankSprite? rankSprite;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Y;
        AutoSizeAxes = Axes.X;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        ruleset.BindValueChanged(_ => updateSubscription(), true);
    }

    private void updateSubscription()
    {
        disposeSubscription();

        if (beatmap is null)
        {
            updateRankDisplay(null);
            return;
        }

        scoreSubscription = realm.RegisterForNotifications(r =>
                r.GetAllLocalScoresForUser(api.LocalUser.Value.Id)
                 .Filter($@"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0"
                         + $" && {nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $1", beatmap.ID, ruleset.Value.ShortName),
            localScoresChanged);
    }

    private void updateRankDisplay(ScoreInfo? topScore)
    {
        if (rankSprite?.Rank == topScore?.Rank)
            return;

        if (rankSprite is not null)
        {
            RemoveInternal(rankSprite, false);
            rankSprite = null;
        }

        // Failed scores and no scores do not get a rank displayed.
        if (topScore is null || topScore.Rank < ScoreRank.D)
            return;

        rankSprite = getRankSprite(topScore.Rank);
        AddInternal(rankSprite);
    }

    private void localScoresChanged(IRealmCollection<ScoreInfo> sender, ChangeSet? changes)
    {
        // This subscription may fire from changes to linked beatmaps, which we don't care about.
        // It's currently not possible for a score to be modified after insertion, so we can safely ignore callbacks with only modifications.
        if (changes?.HasCollectionChanges() == false)
            return;

        ScoreInfo? topScore = sender.MaxBy(info => (info.TotalScore, -info.Date.UtcDateTime.Ticks));
        updateRankDisplay(topScore);
    }

    private void disposeSubscription()
    {
        scoreSubscription?.Dispose();
        scoreSubscription = null;
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        disposeSubscription();
    }

    private LegacyRankSprite getRankSprite(ScoreRank rank)
    {
        var sprite = rankSpritePool?.Get(rank)
            ?? new LegacyRankSprite(rank); // fallback in case the pool is not available, e.g. test scenarios

        sprite.Anchor = Anchor.Centre;
        sprite.Origin = Anchor.Centre;

        return sprite;
    }
}
