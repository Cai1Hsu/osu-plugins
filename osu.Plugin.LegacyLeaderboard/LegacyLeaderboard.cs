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
using osu.Framework.Utils;
using osu.Game.Plugins;
using System.Runtime.CompilerServices;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboard : CompositeDrawable, ISerialisableDrawable
{
    private const float stable_ratio = 1.6f;
    private const float entry_height = 36f * stable_ratio; // spacing included in stable

    public bool UsesFixedAnchor { get; set; } = true;

    [Resolved]
    private IGameplayLeaderboardProvider leaderboardProvider { get; set; } = null!;

    [Resolved]
    private TextureStore textures { get; set; } = null!;

    [SettingSource("Max Entries", "The maximum number of entries to show on the leaderboard.")]
    public Bindable<int> MaxEntries { get; private set; } = new BindableInt(6)
    {
        MinValue = 1,
        // Limited by CalculateEntryTransparency, entries beyond 24 will be fully transparent.
        MaxValue = 24,
        Default = 6, // stable's default
    };

    private readonly IBindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();

    private Container<LegacyLeaderboardEntry> entriesContainer = null!;

    public LegacyLeaderboard()
    {
        Anchor = Anchor.CentreLeft;
        Origin = Anchor.CentreLeft;
    }

    private Container explosionContainer = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        scores.BindTo(leaderboardProvider.Scores);

        InternalChildren = new Drawable[]
        {
            explosionContainer = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            },
            entriesContainer = new Container<LegacyLeaderboardEntry>
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
            trackingEntry = null;
            lastTrackingPosition = null;

            entriesContainer.Clear();

            foreach (var score in scores)
                AddScore(score);
        }, true);
    }

    private LegacyLeaderboardEntry? trackingEntry;
    private int? lastTrackingPosition;

    public void AddScore(GameplayLeaderboardScore score)
    {
        var entry = CreateEntry(score);

        if (score.Tracked)
        {
            Debug.Assert(trackingEntry is null);
            trackingEntry = entry;
        }

        entriesContainer.Add(entry);
        entry.Y = -entry_height; // start above the visible area
        entry.Alpha = 0;
        entry.VisibleInLeaderboard.Value = false;

        // Don't display immediately, position info is still unavailable.
        // Provider will trigger event when ready.
        entry.ScorePosition.BindValueChanged(_ => Scheduler.AddOnce(sort));
        entry.ProviderDisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort));
    }

    private List<LegacyLeaderboardEntry> sortDisplayedEntries(SortedList<LegacyLeaderboardEntry> scoreSorted)
    {
        var maxEntries = Math.Max(1, MaxEntries.Value);

        int capacity = Math.Min(scoreSorted.Count, maxEntries);
        var displayedEntries = new List<LegacyLeaderboardEntry>(capacity);

        // always add first place, but if we only have one slot, ensure tracking is prioritised
        if (scoreSorted.FirstOrDefault() is { } firstPlace &&
            (!firstPlace.IsTracking || maxEntries > 1 || trackingEntry is null))
        {
            displayedEntries.Add(firstPlace);

            if (displayedEntries.Count >= maxEntries)
                return displayedEntries;
        }

        // try fill the reset from tracking entry upwards
        int trackingIndex = trackingEntry is null ? -1 : scoreSorted.IndexOf(trackingEntry);
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
        Debug.Assert(trackingEntry is null || displayedEntries.Contains(trackingEntry));
        Debug.Assert(scoreSorted.Count <= MaxEntries.Value || displayedEntries.Count == maxEntries);

        return displayedEntries;
    }

    private static int CompareEntries(LegacyLeaderboardEntry x, LegacyLeaderboardEntry y)
    {
        if (x.ScorePosition.Value.HasValue && y.ScorePosition.Value.HasValue)
        {
            int positionComparison = x.ScorePosition.Value.Value.CompareTo(y.ScorePosition.Value.Value);
            if (positionComparison != 0)
                return positionComparison;
        }

        int scoreComparison = y.TotalScore.Value.CompareTo(x.TotalScore.Value);
        if (scoreComparison != 0)
            return scoreComparison;

        // quitters go to the bottom
        int quitComparison = x.HasQuit.Value.CompareTo(y.HasQuit.Value);
        if (quitComparison != 0)
            return quitComparison;

        // tracking should have priority when all else is equal
        int trackingComparison = y.IsTracking.CompareTo(x.IsTracking);
        if (trackingComparison != 0)
            return trackingComparison;

        // Don't compare ProviderDisplayOrder as tracking's may be 0

        int xUserId = x.User?.OnlineID ?? int.MinValue;
        int yUserId = y.User?.OnlineID ?? int.MinValue;
        int userIdComparison = xUserId.CompareTo(yUserId);
        if (userIdComparison != 0)
            return userIdComparison;

        // Compare by reference as a last resort to ensure a stable sort.
        return RuntimeHelpers.GetHashCode(x).CompareTo(RuntimeHelpers.GetHashCode(y));
    }

    private const float transition_duration = 600;

    private static readonly IComparer<LegacyLeaderboardEntry> comparer = Comparer<LegacyLeaderboardEntry>.Create(CompareEntries);

    private void sort()
    {
        var sorted = new SortedList<LegacyLeaderboardEntry>(comparer);

        sorted.AddRange(entriesContainer.Children);

        // make higher score look closer to front
        for (int i = 0; i < sorted.Count; i++)
            entriesContainer.ChangeChildDepth(sorted[i], i);

        var displayedEntries = sortDisplayedEntries(sorted);

        // handle entries to be displayed
        for (int i = 0; i < displayedEntries.Count; i++)
        {
            var entry = displayedEntries[i];

            if (entry.IsTracking &&
                lastTrackingPosition.HasValue &&
                entry.ScorePosition.Value.HasValue &&
                entry.ScorePosition.Value < lastTrackingPosition.Value)
                FlashExplosionAt(entry);

            entry.VisibleInLeaderboard.Value = true;
            entry.LeaderboardDisplayIndex.Value = i;

            entry.FadeTo(CalculateEntryTransparency(i), transition_duration)
                .MoveToY(i * entry_height, transition_duration, Easing.Out);
        }

        if (trackingEntry is not null)
            lastTrackingPosition = trackingEntry.ScorePosition.Value;

        int trackingIndex = trackingEntry is null ? -1 : sorted.IndexOf(trackingEntry);

        // first invisible after last displayed
        // FIXME: investigate how stable actually handles this case
        int invisibleIndex = displayedEntries.Count;

        // handle entries to be hidden
        for (int i = 0; i < sorted.Count; i++)
        {
            var entry = sorted[i];

            // displayed entries are sorted, safely use binary search to improve performance
            int sortedIndex = displayedEntries.BinarySearch(entry, comparer);

            if (sortedIndex >= 0)
                continue;

            bool previouslyInvisible = !entry.VisibleInLeaderboard.Value;

            entry.VisibleInLeaderboard.Value = false;
            int newLeaderboardIndex = (trackingIndex < 0 || i < trackingIndex)
                    ? 0 // ensure high scores appears from top
                    : invisibleIndex;

            entry.LeaderboardDisplayIndex.Value = newLeaderboardIndex;

            float targetY = newLeaderboardIndex * entry_height;

            // no need to animate if it was already invisible
            if (previouslyInvisible)
            {
                // Only set position after fully faded out to avoid visual popping.
                if (Precision.AlmostEquals(entry.Alpha, 0))
                    // if leaderboard's size adjusted, ensure new position is applied.
                    entry.Y = targetY;

                continue;
            }

            entry.FadeOut(transition_duration)
                .MoveToY(targetY, transition_duration, Easing.Out);
        }
    }

    private bool IsRightSideLayout => Anchor.HasFlagFast(Anchor.x2);

    public void FlashExplosionAt(LegacyLeaderboardEntry? entry = null)
    {
        entry ??= trackingEntry;

        ArgumentNullException.ThrowIfNull(entry);

        entry.FlashBackground();

        var position = entry.Position + new Vector2(0, entry.Height / 2);
        var scale = Vector2.One;

        if (IsRightSideLayout)
        {
            position.X += entry.Width;
            scale.X = -1;
        }

        Sprite explosion2 = new Sprite
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Position = position,
            Scale = scale,
            Texture = textures.GetAutoSized("UI/scoreboard-explosion-2"),
            Blending = BlendingParameters.Additive,
        };

        explosionContainer.Add(explosion2);

        explosion2
            .ScaleTo(new Vector2(16, 1.2f), 200, Easing.Out)
            .FadeOut(400)
            .Expire();

        Sprite explosion1 = new Sprite
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Position = position,
            Scale = scale,
            Texture = textures.GetAutoSized("UI/scoreboard-explosion-1"),
            Blending = BlendingParameters.Additive,
        };

        explosionContainer.Add(explosion1);

        explosion1
            .ScaleTo(new Vector2(1, 1.3f), 200, Easing.Out)
            .FadeOutFromOne(700)
            .Expire();
    }

    public float CalculateEntryTransparency(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (index >= MaxEntries.Value || index > entriesContainer.Count - 1)
            return 0;

        return MathF.Max(0, 0.8f - (index * (0.1f / 3)));
    }

    protected virtual LegacyLeaderboardEntry CreateEntry(GameplayLeaderboardScore score)
        => new LegacyLeaderboardEntry(score);
}
