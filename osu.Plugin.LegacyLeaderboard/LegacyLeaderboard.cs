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

    private readonly IBindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();

    private readonly SortedList<DisplayScoreItem> displayScores = new SortedList<DisplayScoreItem>(comparer);

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
        scores.BindTo(leaderboardProvider.Scores);

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

        scores.BindCollectionChanged((_, _) =>
        {
            clearScores();
            entriesContainer.Clear(false); // don't dispose poolables, return to pool instead

            foreach (var score in scores)
                AddScore(score);
        }, true);
    }

    private void clearScores()
    {
        trackingScore = null;
        lastTrackingPosition = -1;

        foreach (var displayScore in displayScores)
            displayScore.Dispose();

        displayScores.Clear();
    }

    private DisplayScoreItem? trackingScore;
    private int lastTrackingPosition;

    public void AddScore(GameplayLeaderboardScore providerScore)
    {
        var displayScore = new DisplayScoreItem(providerScore);

        displayScores.Add(displayScore);

        if (providerScore.Tracked)
            // multiple tracking score is not expected.
            // but in case it happens, prefer the first one.
            trackingScore ??= displayScore;

        displayScore.YPosition = -entry_height; // start above the visible area

        // Don't display immediately, position info is still unavailable.
        // Provider will trigger event when ready.
        displayScore.ScorePosition.BindValueChanged(_ => Scheduler.AddOnce(sort));
        displayScore.ProviderDisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort));
    }

    private List<DisplayScoreItem> sortDisplayedEntries(SortedList<DisplayScoreItem> scoreSorted)
    {
        var maxEntries = Math.Max(1, MaxEntries.Value);

        int capacity = Math.Min(scoreSorted.Count, maxEntries);
        var displayedEntries = new List<DisplayScoreItem>(capacity);

        // always add first place, but if we only have one slot, ensure tracking is prioritised
        if (scoreSorted.FirstOrDefault() is { } firstPlace && (
            firstPlace.GameplayScore.Tracked || // first place is tracking
            trackingScore is null || // tracking not present
            maxEntries > 1 // has enough slot for tracking
        ))
        {
            displayedEntries.Add(firstPlace);

            if (displayedEntries.Count >= maxEntries)
                return displayedEntries;
        }

        // try fill the reset from tracking entry upwards
        int trackingIndex = trackingScore is null ? -1 : scoreSorted.IndexOf(trackingScore);
        int fillStartIndex = displayedEntries.Count; // start from where we left off

        // if tracking is the first place, we have already added it.
        // Start filling from index 1 as normal.
        if (trackingIndex > 0)
        {
            int remainingSlots = maxEntries - displayedEntries.Count;

            // Ensure tracking entry is always shown.
            fillStartIndex = Math.Max(fillStartIndex, trackingIndex - remainingSlots + 1);
        }

        // Sequentially add entries to ensure stable ordering.
        // This should keep place ordering consistent with the sorted list.
        while (fillStartIndex < scoreSorted.Count && displayedEntries.Count < maxEntries)
        {
            var entry = scoreSorted[fillStartIndex++];
            displayedEntries.Add(entry);
        }

        Debug.Assert(displayedEntries.Count <= maxEntries);
        Debug.Assert(trackingScore is null || displayedEntries.Contains(trackingScore));
        Debug.Assert(scoreSorted.Count <= maxEntries || displayedEntries.Count == maxEntries);

        return displayedEntries;
    }

    private static int CompareEntries(DisplayScoreItem x, DisplayScoreItem y)
    {
        if (x.ScorePosition.Value.HasValue && y.ScorePosition.Value.HasValue)
        {
            int positionComparison = x.ScorePosition.Value.Value.CompareTo(y.ScorePosition.Value.Value);
            if (positionComparison != 0)
                return positionComparison;
        }

        GameplayLeaderboardScore a = x.GameplayScore;
        GameplayLeaderboardScore b = y.GameplayScore;

        int scoreComparison = b.TotalScore.Value.CompareTo(a.TotalScore.Value);
        if (scoreComparison != 0)
            return scoreComparison;

        // quitters go to the bottom
        int quitComparison = a.HasQuit.Value.CompareTo(b.HasQuit.Value);
        if (quitComparison != 0)
            return quitComparison;

        // tracking should have priority when all else is equal
        int trackingComparison = b.Tracked.CompareTo(a.Tracked);
        if (trackingComparison != 0)
            return trackingComparison;

        // Don't compare ProviderDisplayOrder as tracking's may be 0

        return a.TotalScoreTiebreaker.CompareTo(b.TotalScoreTiebreaker);
    }

    private const float transition_duration = 600;

    private static readonly IComparer<DisplayScoreItem> comparer = Comparer<DisplayScoreItem>.Create(CompareEntries);

    // make higher score look closer to front
    private void updateEntryDepth(DisplayScoreItem scoreItem, float? depth = null)
    {
        if (scoreItem.Model is not PoolableLeaderboardEntry entry)
            return;

        entriesContainer.ChangeChildDepth(entry, depth ?? scoreItem.LeaderboardDisplayIndex.Value);
    }

    private void sort()
    {
        displayScores.Sort();

        var displayedScores = sortDisplayedEntries(displayScores);

        int trackingIndex = trackingScore is null ? -1 : displayScores.IndexOf(trackingScore);

        // handle entries to be displayed
        for (int i = 0; i < displayedScores.Count; i++)
        {
            var score = displayedScores[i];

            if (score.GameplayScore.Tracked &&
                // negative lastTrackingPosition indicates first sort after reset
                // we don't want to flash in this case
                lastTrackingPosition > 0 &&
                trackingIndex < lastTrackingPosition)
                FlashExplosionAt(score);

            if (score.Model is not PoolableLeaderboardEntry pooledEntry)
            {
                pooledEntry = entryPool.Get();

                entriesContainer.Add(pooledEntry);
                pooledEntry.BindScoreItem(score);
            }

            score.VisibleInLeaderboard.Value = true;
            score.LeaderboardDisplayIndex.Value = i;

            float targetY = i * entry_height;
            score.YPosition = targetY;

            updateEntryDepth(score);

            pooledEntry
                .FadeTo(CalculateEntryTransparency(i), transition_duration)
                .MoveToY(targetY, transition_duration, Easing.Out);
        }

        if (trackingScore is not null)
            lastTrackingPosition = trackingIndex;

        // first invisible after last displayed
        // FIXME: investigate how stable actually handles this case
        int invisibleIndex = displayedScores.Count;

        // handle entries to be hidden
        for (int i = 0; i < displayScores.Count; i++)
        {
            var score = displayScores[i];

            // displayed entries are sorted, safely use binary search to improve performance
            int displayIndex = displayedScores.BinarySearch(score, comparer);

            if (displayIndex >= 0)
                continue;

            score.VisibleInLeaderboard.Value = false;
            int newLeaderboardIndex = ((-displayIndex) < invisibleIndex)
                    ? 0 // ensure high scores appears from top
                    : invisibleIndex;

            score.LeaderboardDisplayIndex.Value = newLeaderboardIndex;

            float targetY = newLeaderboardIndex * entry_height;
            score.YPosition = targetY;

            if (score.Model is not PoolableDrawable model)
                continue;

            // update depth to make animation smoother
            // make fading out entries appear at bottom of any existing ones
            updateEntryDepth(score, displayScores.Count + 1024); // very large depth to ensure it's at the back

            model
                .FadeOut(transition_duration)
                .MoveToY(targetY, transition_duration, Easing.Out)
                .Expire();
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
        private LegacyLeaderboard leaderboard;
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
