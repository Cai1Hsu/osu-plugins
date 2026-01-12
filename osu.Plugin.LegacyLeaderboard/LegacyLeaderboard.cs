using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;
using System.Diagnostics;
using osu.Framework.Lists;
using osuTK;
using osu.Game.Configuration;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Extensions.EnumExtensions;
using osu.Game.Plugins;
using osu.Framework.Graphics.Pooling;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboard : CompositeDrawable, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;
    private const float entry_height = 36f * stable_ratio; // spacing included in stable

    public bool UsesFixedAnchor { get; set; } = true;

    [Resolved]
    private IGameplayLeaderboardProvider leaderboardProvider { get; set; } = null!;

    [SettingSource("Max Entries", "The maximum number of entries to show on the leaderboard.")]
    public Bindable<int> MaxEntries { get; private set; } = new BindableInt(6)
    {
        MinValue = 1,
        // Limited by CalculateEntryTransparency, entries beyond 24 will be fully transparent.
        MaxValue = 24,
        Default = 6, // stable's default
    };

    private readonly IBindableList<GameplayLeaderboardScore> scoresList = new BindableList<GameplayLeaderboardScore>();

    private readonly List<DisplayScoreItem> scores = new List<DisplayScoreItem>();

    private Container<PoolableLeaderboardEntry> entriesContainer = null!;

    public LegacyLeaderboard()
    {
        Anchor = Anchor.CentreLeft;
        Origin = Anchor.CentreLeft;
    }

    private Container explosionContainer = null!;
    private EntryPool entryPool = null!;

    private DrawablePool<Explosion2> explosion2Pool = null!;
    private DrawablePool<Explosion1> explosion1Pool = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        scoresList.BindTo(leaderboardProvider.Scores);

        InternalChildren = new Drawable[]
        {
            entryPool = new EntryPool(this, MaxEntries.Value),
            explosion2Pool = new DrawablePool<Explosion2>(1),
            explosion1Pool = new DrawablePool<Explosion1>(1),
            explosionContainer = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            },
            entriesContainer = new Container<PoolableLeaderboardEntry>
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            }
        };

        MaxEntries.BindValueChanged(_ =>
        {
            updateSize();
            Scheduler.AddOnce(sort);
        });
    }

    private void updateSize()
    {
        Vector2 size = new Vector2(LegacyLeaderboardEntry.WIDTH, entry_height * MaxEntries.Value);

        entriesContainer.Size = size;
        explosionContainer.Size = size;
        Size = size;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        updateSize();

        scoresList.BindCollectionChanged((_, _) =>
        {
            clearScores();
            entriesContainer.Clear(false); // don't dispose poolables, return to pool instead

            foreach (var score in scoresList)
                AddScore(score);

            trackingScore?.ProviderDisplayOrder.BindValueChanged(_ => Scheduler.Add(handleTrackingExplosion));
        }, true);
    }

    private void clearScores()
    {
        trackingScore = null;
        lastTrackingPosition = -1;

        foreach (var displayScore in scores)
            displayScore.Dispose();

        scores.Clear();
    }

    private DisplayScoreItem? trackingScore;
    private long lastTrackingPosition;

    public void AddScore(GameplayLeaderboardScore providerScore)
    {
        var displayScore = new DisplayScoreItem(providerScore);

        scores.Add(displayScore);

        if (providerScore.Tracked)
            // multiple tracking score is not expected.
            // but in case it happens, prefer the first one.
            trackingScore ??= displayScore;

        displayScore.YPosition = -entry_height; // start above the visible area

        // Don't display immediately, position info is still unavailable.
        // Provider will trigger event when ready.
        displayScore.ScorePosition.BindValueChanged(_ => Scheduler.AddOnce(sort));
        displayScore.ProviderDisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort));

        // in case position is already available, sort immediately.
        if (displayScore.ProviderDisplayOrder.Value is not 0) // 0 is default uninitialised value, the order is 1-based.
            Scheduler.AddOnce(sort);
    }

    private const float transition_duration = 600;

    // make higher score look closer to front
    private void updateEntryDepth(DisplayScoreItem scoreItem, float depth)
    {
        if (scoreItem.Model is not PoolableLeaderboardEntry entry)
            return;

        entriesContainer.ChangeChildDepth(entry, depth);
    }

    /// <summary>
    /// Get the display index of a score item.
    /// Returns -1 indicating the score is too high too be displayed.
    /// Other negative values indicating to low to be displayed, the magnitude is the would-be index.
    /// </summary>
    /// <param name="score">The score item to get index for.</param>
    /// <param name="displayCount">The maximum number of entries to be displayed.</param>
    /// <returns>The display index, negative value indicates not displayed.</returns>
    private int GetScoreDisplayIndex(DisplayScoreItem score, int displayCount)
    {
        var providerDisplayOrderIndex = score.ProviderDisplayOrder.Value - 1;

        if (providerDisplayOrderIndex < 0)
            return -1; // uninitialized

        if (displayCount > 1)
        {
            if (providerDisplayOrderIndex is 0)
                return 0; // first place

            long cutoffBegin = 1; // if no tracking, display higher scores as possible
            long remainingSlot = displayCount - 1; // first place already taken

            if (trackingScore is not null)
            {
                // ensure tracking is always displayed, so cutoff index is based on its position
                long trackingIndex = trackingScore.ProviderDisplayOrder.Value - 1;

                Debug.Assert(trackingIndex >= 0); // don't call this method when tracking is uninitialised

                cutoffBegin = Math.Max(cutoffBegin, trackingIndex - remainingSlot + 1);
            }

            if (providerDisplayOrderIndex < cutoffBegin)
                return -1; // too high to be displayed yet

            int displayIndex = (int)(providerDisplayOrderIndex - cutoffBegin) + 1; // +1 for first places

            if (displayIndex < displayCount)
                return displayIndex;

            return -displayIndex; // too low to be displayed
        }

        return score.GameplayScore.Tracked
            ? 0
            : -1; // semantic value is unnecessary, as only one entry is shown, and always the tracking one.
    }

    private const int very_large_depth = 1024; // we never have so many entries displayed

    private void handleInvisibleScore(DisplayScoreItem score, int displayIndex, int invisibleIndex)
    {
        Debug.Assert(displayIndex < 0);

        
        score.VisibleInLeaderboard.Value = false;

        int newLeaderboardIndex = displayIndex == -1
                ? 0 // ensure high scores appears from top
                : invisibleIndex;

        score.LeaderboardDisplayIndex.Value = newLeaderboardIndex;

        float targetY = newLeaderboardIndex * entry_height;
        score.YPosition = targetY;

        if (score.Model is PoolableLeaderboardEntry model)
        {
            // update depth to make animation smoother
            // make fading out entries appear at bottom of any existing ones
            updateEntryDepth(score, very_large_depth - scores.Count); // very large depth to ensure it's at the back
                                                                      // revert depth to make a smoother animation.
                                                                      // since the destination is the same, lower scores look move slower,
                                                                      // if they are covered by faster moving higher scores it looks jarring.

            model
                .FadeOut(transition_duration)
                .MoveToY(targetY, transition_duration, Easing.Out)
                .Expire();
        }
    }

    private void handleVisibleScore(DisplayScoreItem score, int displayIndex, int displayCount)
    {
        if (score.Model is not PoolableLeaderboardEntry pooledEntry)
        {
            pooledEntry = entryPool.Get();

            entriesContainer.Add(pooledEntry);
            pooledEntry.BindScoreItem(score);
        }

        score.VisibleInLeaderboard.Value = true;
        score.LeaderboardDisplayIndex.Value = displayIndex;

        float targetY = displayIndex * entry_height;
        score.YPosition = targetY;

        // we want to make first place appear on top
        // tracking at second to make sure it's visible
        // Then, we want to make LOWER scores appear above higher scores
        float newDepth = displayIndex == 0 ? -2 : // first place
                        (score.GameplayScore.Tracked ? -1 : // tracking
                        (displayCount - displayIndex)); // lower scores above higher scores

        updateEntryDepth(score, newDepth);

        pooledEntry
            .FadeTo(CalculateEntryTransparency(displayIndex), transition_duration)
            .MoveToY(targetY, transition_duration, Easing.Out);
    }

    private void handleTrackingExplosion()
    {
        Debug.Assert(trackingScore is not null);

        var trackingPosition = trackingScore.ProviderDisplayOrder.Value - 1;

        if (trackingPosition < lastTrackingPosition)
            FlashExplosionAt(trackingScore);

        lastTrackingPosition = trackingPosition;
    }

    private void sort()
    {
        int displayCount = Math.Max(1, MaxEntries.Value);

        // Bindable should never be less than 1 due to min value restriction.
        Debug.Assert(displayCount >= 1);

        // skip this sort, leaderboard not ready yet
        if (trackingScore?.ProviderDisplayOrder.Value is 0)
            return;

        // first invisible after last displayed
        // FIXME: investigate how stable actually handles this case
        int invisibleIndex = displayCount;

        for (int i = 0; i < scores.Count; i++)
        {
            var score = scores[i];
            int displayIndex = GetScoreDisplayIndex(score, displayCount);

            if (displayIndex < 0)
            {
                handleInvisibleScore(score, displayIndex, invisibleIndex);
            }
            else
            {
                handleVisibleScore(score, displayIndex, displayCount);
            }
        }
    }

    private bool IsRightSideLayout => Anchor.HasFlagFast(Anchor.x2);

    internal void FlashExplosionAt(DisplayScoreItem? score = null)
    {
        score ??= trackingScore;

        ArgumentNullException.ThrowIfNull(score);

        if (score.Model is not PoolableLeaderboardEntry entry)
            return;

        entry.Drawable.FlashBackground();

        var position = entry.Position + new Vector2(0, entry.Height / 2);
        var scale = Vector2.One;

        if (IsRightSideLayout)
        {
            position.X += entry.Width;
            scale.X = -1;
        }

        var explosion2 = explosion2Pool.Get();
        explosionContainer.Add(explosion2);
        explosion2.Apply(position, scale);

        var explosion1 = explosion1Pool.Get();
        explosionContainer.Add(explosion1);
        explosion1.Apply(position, scale);
    }

    public static float CalculateEntryTransparency(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        // TODO: stable uses `inputCount` thing here to determine transparency curve.
        // See reference source in #13's description.
        // However, the following calculation matches my measurements.
        return MathF.Max(0, 0.8f - (index * (0.1f / 3)));
    }

    protected virtual LegacyLeaderboardEntry CreateEntry()
        => new LegacyLeaderboardEntry();

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        // ensure bound events cleared to avoid memory leaks
        clearScores();
    }

    private partial class EntryPool : DrawablePool<PoolableLeaderboardEntry>
    {
        private readonly LegacyLeaderboard leaderboard;
        public EntryPool(LegacyLeaderboard leaderboard, int initialSize, int? maximumSize = null)
            : base(initialSize, maximumSize)
        {
            this.leaderboard = leaderboard;
        }

        protected override PoolableLeaderboardEntry CreateNewDrawable()
            => new PoolableLeaderboardEntry(leaderboard.CreateEntry());
    }

    private partial class Explosion2 : PoolableDrawable
    {
        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            InternalChild = new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.CentreLeft,
                Texture = textures.GetAutoSized("UI/scoreboard-explosion-2"),
                Blending = BlendingParameters.Additive,
            };
        }

        public void Apply(Vector2 position, Vector2? scale)
        {
            Position = position;
            Scale = scale ?? Vector2.One;

            this.ScaleTo(new Vector2(16, 1.2f), 200, Easing.Out)
                .FadeOutFromOne(400)
                .Expire();
        }
    }

    private partial class Explosion1 : PoolableDrawable
    {
        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            InternalChild = new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.CentreLeft,
                Texture = textures.GetAutoSized("UI/scoreboard-explosion-1"),
                Blending = BlendingParameters.Additive,
            };
        }

        public void Apply(Vector2 position, Vector2? scale)
        {
            Position = position;
            Scale = scale ?? Vector2.One;

            this.ScaleTo(new Vector2(1, 1.3f), 200, Easing.Out)
                .FadeOutFromOne(700)
                .Expire();
        }
    }
}
