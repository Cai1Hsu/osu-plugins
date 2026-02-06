using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;
using System.Diagnostics;
using osuTK;
using osu.Game.Configuration;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Extensions.EnumExtensions;
using osu.Game.Plugins;
using osu.Framework.Graphics.Pooling;
using osu.Game.Screens.Play;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Framework.Localisation;
using osu.Plugin.LegacyExperience.Localisations;
using osu.Game.Input.Bindings;
using osu.Game.Input;
using osu.Game.Online.Leaderboards;

namespace osu.Plugin.LegacyExperience.Leaderboards;

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

    [Resolved]
    private LeaderboardManager leaderboardManager { get; set; } = null!;

    private Container explosionContainer = null!;
    private EntryPool entryPool = null!;

    private DrawablePool<Explosion2> explosion2Pool = null!;
    private DrawablePool<Explosion1> explosion1Pool = null!;

    private readonly IBindable<LocalUserPlayingState> localUserPlayingState = new Bindable<LocalUserPlayingState>();
    private IBindable<bool> showLeaderboardConfig = null!;

    private readonly Bindable<bool> visibility = new BindableBool();

    private Container content = null!;
    private OsuSpriteText? tipText;

    [BackgroundDependencyLoader]
    private void load(ILocalUserPlayInfo localUserPlayInfo, OsuConfigManager osuConfig)
    {
        scoresList.BindTo(leaderboardProvider.Scores);

        AddRangeInternal(new Drawable[]
        {
            entryPool = new EntryPool(this, MaxEntries.Value),
            explosion2Pool = new DrawablePool<Explosion2>(1),
            explosion1Pool = new DrawablePool<Explosion1>(1),
            content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    explosionContainer = new Container
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.Both,
                    },
                    entriesContainer = new Container<PoolableLeaderboardEntry>
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.Both,
                    }
                }
            }
        });

        MaxEntries.BindValueChanged(_ =>
        {
            updateSize();
            Scheduler.AddOnce(sort);
        });

        localUserPlayingState.BindTo(localUserPlayInfo.PlayingState);
        showLeaderboardConfig = osuConfig.GetBindable<bool>(OsuSetting.GameplayLeaderboard);

        showLeaderboardConfig.BindValueChanged(_ => updateVisibilityValue(true));
        localUserPlayingState.BindValueChanged(_ => updateVisibilityValue(false), true);

        visibility.BindValueChanged(_ => updateVisibility());
        content.Alpha = visibility.Value ? 1 : 0; // animation in updateVisibility is scheduled, so set initial value here to avoid visual glitch.
    }

    private void updateVisibilityValue(bool showTip)
    {
        visibility.Value = localUserPlayingState.Value switch
        {
            LocalUserPlayingState.Break => true,
            LocalUserPlayingState.NotPlaying or
            LocalUserPlayingState.Playing => showLeaderboardConfig.Value,
            _ => throw new ArgumentOutOfRangeException(),
        };

        if (!showTip)
            return;

        if (visibility.Value != showLeaderboardConfig.Value && !showLeaderboardConfig.Value)
            displayTip(LegacyStrings.Player_ScoreBoardShowStatus);
        else if (visibility.Value)
            displayTip(LegacyStrings.Player_ScoreBoardShowStatus2);
    }

    private void updateVisibility()
    {
        // stable updates alpha by 0.08 every 16.6ms.
        const double frame_duration = 1000.0 / 60;
        const double transition_count = 1 / 0.08;
        const double full_transition_duration = frame_duration * transition_count;

        Scheduler.Add(() =>
        {
            var targetAlpha = visibility.Value ? 1 : 0;
            var delta = Math.Abs(content.Alpha - targetAlpha);

            content.FadeTo(targetAlpha, full_transition_duration * delta);
        });
    }

    private void displayTip(LocalisableString text)
    {
        Scheduler.Add(() =>
        {
            tipText?.FadeOut(100)
                .Expire();

            content.Add(tipText = new OsuSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.BottomLeft,
                BypassAutoSizeAxes = Axes.Both,
                Font = OsuFont.Default.With(size: 12 * stable_ratio, weight: FontWeight.SemiBold),
                Text = text,
            });

            tipText.FadeOutFromOne(6000)
                .Expire();
        });
    }

    private void updateSize()
    {
        Size = new Vector2(LegacyLeaderboardEntry.WIDTH, entry_height * MaxEntries.Value);
    }

    [Resolved]
    private RealmKeyBindingStore keyBindingStore { get; set; } = null!;

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

            if (trackingScore is not null)
                trackingDisplayOrder.BindTo(trackingScore.ProviderDisplayOrder);

            // we don't want to spam tip when scores are being loaded, so only show tip when the first batch of scores are loaded.
            if (leaderboardManager.Scores.Value?.IsPartial ?? true)
                return;

            Scheduler.Add(() =>
            {
                if (scoresList.Count > 0 && showLeaderboardConfig.Value)
                {
                    displayTip(LegacyStrings.Player_ToggleScoreboard(
                        keyBindingStore.GetBindingsStringFor(GlobalAction.ToggleInGameLeaderboard)));
                }
            });
        }, true);

        trackingDisplayOrder.BindValueChanged(handleTrackingExplosion);
    }

    private void clearScores()
    {
        trackingScore = null;
        trackingDisplayOrder.UnbindBindings();
        trackingDisplayOrder.Value = -1;

        foreach (var displayScore in scores)
            displayScore.Dispose();

        scores.Clear();
    }

    private DisplayScoreItem? trackingScore;
    private readonly BindableLong trackingDisplayOrder = new BindableLong();

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
        if (displayScore.ScorePosition.Value.HasValue)
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
    private long GetScoreDisplayIndex(DisplayScoreItem score, int displayCount, long firstPositionIndex)
    {
        var providerDisplayOrderIndex = score.ProviderDisplayOrder.Value;

        if (providerDisplayOrderIndex < 0)
            return -1; // uninitialized

        if (displayCount > 1)
        {
            if (providerDisplayOrderIndex == firstPositionIndex)
                return 0; // first place

            long cutoffBegin = firstPositionIndex + 1; // if no tracking, display higher scores as possible
            long remainingSlot = displayCount - 1; // first place already taken

            if (trackingScore is not null)
            {
                // ensure tracking is always displayed, so cutoff index is based on its position
                long trackingIndex = trackingScore.ProviderDisplayOrder.Value;

                Debug.Assert(trackingIndex >= 0); // don't call this method when tracking is uninitialised

                cutoffBegin = Math.Max(cutoffBegin, trackingIndex - remainingSlot + 1);
            }

            if (providerDisplayOrderIndex < cutoffBegin)
                return -1; // too high to be displayed yet

            long displayIndex = providerDisplayOrderIndex - cutoffBegin + 1; // +1 for first places

            if (displayIndex < displayCount)
                return displayIndex;

            return -displayIndex; // too low to be displayed
        }

        if ((trackingScore is null && providerDisplayOrderIndex == firstPositionIndex) || (trackingScore == score))
            return 0;

        return -1; // semantic value is unnecessary, as only one entry is shown, and always the tracking one.
    }

    private const int very_large_depth = 1024; // we never have so many entries displayed

    private void handleInvisibleScore(DisplayScoreItem score, long displayIndex, int invisibleIndex)
    {
        Debug.Assert(displayIndex < 0);

        score.VisibleInLeaderboard.Value = false;

        int newLeaderboardIndex = displayIndex == -1
                ? 0 // ensure high scores appears from top
                : invisibleIndex;

        // use a negative index to indicate invisible
        score.LeaderboardDisplayIndex.Value = -newLeaderboardIndex;

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

    private void handleVisibleScore(DisplayScoreItem score, long displayIndex, int displayCount)
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

    private void handleTrackingExplosion(ValueChangedEvent<long> @event)
    {
        if (trackingScore is null)
            return;

        // uninitialized
        if (@event.OldValue < 0 || @event.NewValue < 0)
            return;

        if (@event.NewValue < @event.OldValue)
            Scheduler.Add(() => FlashExplosionAt(trackingScore));
    }

    private void sort()
    {
        int displayCount = Math.Max(1, MaxEntries.Value);

        // Bindable should never be less than 1 due to min value restriction.
        Debug.Assert(displayCount >= 1);

        long firstPositionIndex = long.MaxValue;

        for (int i = 0; i < scores.Count; i++)
        {
            var score = scores[i];

            // skip this sort, leaderboard not ready yet
            if (!score.ScorePosition.Value.HasValue)
                return;

            firstPositionIndex = Math.Min(firstPositionIndex, score.ProviderDisplayOrder.Value);
        }

        // first invisible after last displayed
        // FIXME: investigate how stable actually handles this case
        int invisibleIndex = displayCount;

        for (int i = 0; i < scores.Count; i++)
        {
            var score = scores[i];
            long displayIndex = GetScoreDisplayIndex(score, displayCount, firstPositionIndex);

            if (displayIndex < 0)
            {
                // cast to int is safe here, as up to 24 entries are supported
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

    public static float CalculateEntryTransparency(long index)
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
