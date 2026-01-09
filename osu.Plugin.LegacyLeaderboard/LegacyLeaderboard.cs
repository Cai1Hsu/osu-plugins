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

        MaxEntries.BindValueChanged(_ => updateSize(entry_height * MaxEntries.Value), true);
        MaxEntries.BindValueChanged(_ => Scheduler.AddOnce(sort));
    }

    private void updateSize(float height)
    {
        Vector2 size = new Vector2(82 * stable_ratio, height);

        entriesContainer.Size = size;
        explosionContainer.Size = size;
        Size = size;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

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

        entry.ScorePosition.BindValueChanged(_ => Scheduler.AddOnce(sort));
        entry.ProviderDisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort), true);
    }

    private IReadOnlyList<LegacyLeaderboardEntry> sortDisplayedEntries(SortedList<LegacyLeaderboardEntry> scoreSorted)
    {
        var maxEntries = Math.Max(1, MaxEntries.Value);

        var displayedEntries = new List<LegacyLeaderboardEntry>();

        if (scoreSorted.FirstOrDefault() is { } firstPlace && firstPlace != trackingEntry)
            displayedEntries.Add(firstPlace);

        if (displayedEntries.Count >= maxEntries)
            return displayedEntries;

        // try fill the reset from tracking entry upwards
        int trackingIndex = trackingEntry is null ? -1 : scoreSorted.IndexOf(trackingEntry);

        if (trackingIndex < -1)
        {
            trackingIndex = 0; // fill normally from second place
        }
        else if (trackingIndex > 1)
        {
            int remainingSlots = maxEntries - displayedEntries.Count - 1; // reserve one for tracking entry
            int upfillStart = Math.Max(1, trackingIndex - remainingSlots);

            while (upfillStart < trackingIndex && displayedEntries.Count < (maxEntries - 1))
            {
                var entry = scoreSorted[upfillStart++];
                Debug.Assert(entry != trackingEntry);
                displayedEntries.Add(entry);
            }
        }

        if (trackingEntry is not null)
            displayedEntries.Add(trackingEntry);

        int downfillStart = trackingIndex + 1;
        while (downfillStart < scoreSorted.Count && displayedEntries.Count < maxEntries)
        {
            var entry = scoreSorted[downfillStart++];
            Debug.Assert(entry != trackingEntry);
            displayedEntries.Add(entry);
        }

        Debug.Assert(displayedEntries.Count <= maxEntries);
        Debug.Assert(trackingEntry is null || displayedEntries.Contains(trackingEntry));
        Debug.Assert(scoreSorted.Count <= MaxEntries.Value || displayedEntries.Count == maxEntries);

        return displayedEntries;
    }

    private static int CompareEntries(LegacyLeaderboardEntry x, LegacyLeaderboardEntry y)
    {
        int scoreComparison = y.TotalScore.Value.CompareTo(x.TotalScore.Value);
        if (scoreComparison != 0)
            return scoreComparison;

        int quitComparison = y.HasQuit.Value.CompareTo(x.HasQuit.Value);
        if (quitComparison != 0)
            return quitComparison;

        // tracking should have priority when all else is equal
        int trackingComparison = y.IsTracking.CompareTo(x.IsTracking);
        if (trackingComparison != 0)
            return trackingComparison;

        return x.ProviderDisplayOrder.Value.CompareTo(y.ProviderDisplayOrder.Value);
    }

    private const float transition_duration = 600;

    private void sort()
    {
        var sorted = new SortedList<LegacyLeaderboardEntry>(CompareEntries);

        sorted.AddRange(entriesContainer.Children);

        var displayedEntries = sortDisplayedEntries(sorted);

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

        // first invisible after last displayed
        // FIXME: investigate how stable actually handles this case
        int invisibleIndex = displayedEntries.Count;

        for (int i = 0; i < entriesContainer.Count; i++)
        {
            var entry = entriesContainer[i];

            if (displayedEntries.Contains(entry))
                continue;

            bool previouslyInvisible = !entry.VisibleInLeaderboard.Value;

            entry.VisibleInLeaderboard.Value = false;
            int newLeaderboardIndex = entry.ProviderDisplayOrder.Value < trackingEntry?.ProviderDisplayOrder.Value
                ? 0 // ensure high scores appears from top
                : invisibleIndex;

            entry.LeaderboardDisplayIndex.Value = newLeaderboardIndex;

            float targetY = newLeaderboardIndex * entry_height;

            // no need to animate if it was already invisible
            if (previouslyInvisible)
            {
                // Don't set position if still during a fade-out.
                if (Precision.AlmostEquals(entry.Alpha, 0))
                    // if leaderboard's size adjusted, ensure new position is applied.
                    entry.Y = targetY;

                continue;
            }

            entry.FadeOut(transition_duration)
                .MoveToY(targetY, transition_duration, Easing.Out);
        }
    }

    private Texture? GetTexture(string lookup)
        => textures.Get($"{lookup}@2x") ?? textures.Get(lookup);

    private bool IsRightSideLayout => Anchor.HasFlagFast(Anchor.x2);

    public void FlashExplosionAt(LegacyLeaderboardEntry? entry = null)
    {
        entry ??= trackingEntry;

        Debug.Assert(entry is not null);

        entry.FlashBackground();

        var position = entry.Position + new Vector2(0, entry.Height / 2);
        var scale = Vector2.One;

        if (IsRightSideLayout)
        {
            position.X += entry.Width;
            scale.X = -1;
        }

        Sprite explision2 = new Sprite
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Position = position,
            Scale = scale,
            Texture = GetTexture("UI/scoreboard-explosion-2"),
            Blending = BlendingParameters.Additive,
        };

        explosionContainer.Add(explision2);

        explision2
            .ScaleTo(new Vector2(16, 1.2f), 200, Easing.Out)
            .FadeOut(400)
            .Expire();

        Sprite explosion1 = new Sprite
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Position = position,
            Scale = scale,
            Texture = GetTexture("UI/scoreboard-explosion-1"),
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
