// This file is adapted from osu!lazer's PanelLocalRankDisplay to work in the Legacy Experience plugin.
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Original file: https://github.com/ppy/osu/blob/952fd0d493eb3cd9994ea8ff4e27b44e82c1f287/osu.Game/Screens/SelectV2/PanelLocalRankDisplay.cs

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Plugins;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using Realms;

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

    private IDisposable? scoreSubscription;

    private RankSprite rankSprite = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Y;
        AutoSizeAxes = Axes.X;

        InternalChild = rankSprite = new RankSprite
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        ruleset.BindValueChanged(_ => updateSubscription(), true);
    }

    private void updateSubscription()
    {
        disposeSubscription();

        if (beatmap == null)
            return;

        scoreSubscription = realm.RegisterForNotifications(r =>
                r.GetAllLocalScoresForUser(api.LocalUser.Value.Id)
                 .Filter($@"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0"
                         + $" && {nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $1", beatmap.ID, ruleset.Value.ShortName),
            localScoresChanged);
    }

    private void localScoresChanged(IRealmCollection<ScoreInfo> sender, ChangeSet? changes)
    {
        // This subscription may fire from changes to linked beatmaps, which we don't care about.
        // It's currently not possible for a score to be modified after insertion, so we can safely ignore callbacks with only modifications.
        if (changes?.HasCollectionChanges() == false)
            return;

        ScoreInfo? topScore = sender.MaxBy(info => (info.TotalScore, -info.Date.UtcDateTime.Ticks));
        rankSprite.Alpha = topScore != null ? 1 : 0;
        rankSprite.Rank = topScore?.Rank;
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

    private partial class RankSprite : Sprite
    {
        [Resolved]
        private ISkinSource? skin { get; set; }

        [Resolved]
        private TextureStore? textures { get; set; }

        private ScoreRank? rank;

        public ScoreRank? Rank
        {
            get => rank;
            set
            {
                if (rank == value)
                    return;

                rank = value;
                updateTexture();
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (skin is not null)
                skin.SourceChanged += updateTexture;

            updateTexture();
        }

        private void updateTexture()
        {
            if (rank is null)
            {
                Texture = null;
                return;
            }

            var lookup = getRankTextureLookup(rank.Value);
            Texture = skin?.GetSkinTexture(lookup, textures, "UI");
        }

        private static string getRankTextureLookup(ScoreRank rank) => rank switch
        {
            ScoreRank.XH => "ranking-XH-small",
            ScoreRank.X => "ranking-X-small",
            ScoreRank.SH => "ranking-SH-small",
            ScoreRank.S => "ranking-S-small",
            ScoreRank.A => "ranking-A-small",
            ScoreRank.B => "ranking-B-small",
            ScoreRank.C => "ranking-C-small",
            ScoreRank.D => "ranking-D-small",
            _ => string.Empty,
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin is not null)
                skin.SourceChanged -= updateTexture;
        }
    }
}
