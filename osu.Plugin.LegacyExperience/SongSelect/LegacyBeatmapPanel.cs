using System.Diagnostics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.SelectV2;
using osuTK;
using osuTK.Graphics;
using SongSelectV2 = osu.Game.Screens.SelectV2.SongSelect;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyBeatmapPanel : LegacyPanel
{
    private OsuSpriteText titleText = null!;
    private OsuSpriteText artistText = null!;
    private OsuSpriteText difficultyText = null!;
    private StarDifficultyDisplay? starDisplay = null!;

    private PanelBeatmapCoverContainer cover = null!;
    private Container playInfoContainer = null!;
    private Container beatmapInfoContainer = null!;

    [Resolved]
    private DrawablePool<StarDifficultyDisplay> starDifficultyPool { get; set; } = null!;

    [Resolved]
    private BeatmapDifficultyCache? difficultyCache { get; set; } = null!;

    [Resolved]
    private BeatmapManager? beatmaps { get; set; } = null!;

    private static readonly Vector2 cover_position = new Vector2(5.2f, 0.25f) * LegacyExperiencePlugin.StableRatio;
    private static readonly Vector2 cover_size = new Vector2(80, 60) * 1.425f;

    private static readonly float info_padding = 75 * LegacyExperiencePlugin.StableRatio - cover_size.X;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(new GridContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
            Padding = new MarginPadding
            {
                Left = cover_position.X,
            },
            ColumnDimensions = new[]
            {
                // cover
                new Dimension(GridSizeMode.AutoSize),
                // play info
                new Dimension(GridSizeMode.AutoSize, minSize: info_padding),
                // beatmap info
                new Dimension(GridSizeMode.Distributed)
            },
            RelativeSizeAxes = Axes.Both,
            Content = new[]
            {
                new Drawable[]
                {
                    // container used to display the cover image
                    cover = new PanelBeatmapCoverContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = cover_size,
                        Position = new Vector2(0, cover_position.Y),
                        Colour = new Color4(50, 50, 50, 255), // inactive color
                        FillMode = FillMode.Fill,
                        Masking = true,
                    },
                    // container used to display play info like
                    // - local best rank
                    // - ruleset icon if not the selected one
                    // Currently unused, but we may add more info later.
                    playInfoContainer = new Container
                    {
                        // currently unused, so 0 size
                        RelativeSizeAxes = Axes.Y,
                        Width = 0,
                        // TODO: investigate anchor/origin
                    },
                    beatmapInfoContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            titleText = new OsuSpriteText()
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.GetFont(size: 16f * LegacyExperiencePlugin.StableRatio),
                                Position = new Vector2(0, -17f) * LegacyExperiencePlugin.StableRatio,
                                AllowMultiline = false,
                                Colour = PanelColors.InactiveText,
                                // Text = "Beatmap Title",
                            },
                            artistText = new OsuSpriteText()
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.GetFont(size: 12f * LegacyExperiencePlugin.StableRatio),
                                Position = new Vector2(1, -7f) * LegacyExperiencePlugin.StableRatio,
                                AllowMultiline = false,
                                Colour = PanelColors.InactiveText,
                                // Text = "Artist // Mapper",
                            },
                            difficultyText = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.GetFont(size: 12f * LegacyExperiencePlugin.StableRatio, weight: FontWeight.Bold),
                                Position = new Vector2(1, 4f) * LegacyExperiencePlugin.StableRatio,
                                AllowMultiline = false,
                                Colour = PanelColors.InactiveText,
                                // Text = "Difficulty Name",
                            },
                        }
                    },
                }
            }
        });

        Selected.BindValueChanged(_ => updatePanelState());
        Expanded.BindValueChanged(_ => updatePanelState(), true);
    }

    protected override void SkinChanged()
    {
        base.SkinChanged();

        Scheduler.Add(updatePanelState);
    }

    private void updatePanelState()
    {
        bool isActivated = Expanded.Value || Selected.Value;
        bool activatedBySet = !isActivated &&
            // FIXME:
            // When not grouped, only beatmaps from expanded set are displayed.
            // However, when beatmaps are grouped, this is unable to determine if the beatmap is from an expanded set.
            Item?.Model is GroupedBeatmap grouped && grouped.Group is null;

        // match stable's behavior:
        // // panels from expanded set but not selected looks darker than even inactive panels.
        var textColor = isActivated ? PanelColors.ActiveText :
                        activatedBySet ? PanelColors.InactiveTextFaded : PanelColors.InactiveText;

        titleText.Colour = textColor;
        artistText.Colour = textColor;
        difficultyText.Colour = textColor;

        var coverColor = isActivated || activatedBySet ? PanelColors.White : PanelColors.InactiveCover;

        cover.FadeColour(coverColor, 300);
    }

    private void clearStarDifficultyDisplay()
    {
        if (starDisplay is null)
            return;

        starDisplay.Current.Value = 0;
        starDisplay.FinishTransforms();

        starDisplay.Expire();
        starDisplay = null;
    }

    private void addStarDifficultyDisplay()
    {
        starDisplay = starDifficultyPool.Get();
        starDisplay.Anchor = Anchor.CentreLeft;
        starDisplay.Origin = Anchor.TopLeft;
        // stable uses 7 as Y offset.
        // We tweaked it to 12 for better visual alignment in lazer
        starDisplay.Position = new Vector2(0, 12f) * LegacyExperiencePlugin.StableRatio;
        beatmapInfoContainer.Add(starDisplay);
    }

    protected override void FreeAfterUse()
    {
        base.FreeAfterUse();

        clearStarDifficultyComputation();
        clearStarDifficultyDisplay();
        cover.ClearBackground();
        background_update_task?.Cancel();
        background_update_task = null;
    }

    private const float background_update_debounce = 500; // will this be too long?
    private ScheduledDelegate? background_update_task;

    protected override void PrepareForUse()
    {
        base.PrepareForUse();

        Debug.Assert(Item is not null);

        var displayPolicy = CreateDisplayPolicy(Item.Model);

        titleText.Text = displayPolicy.Title;
        artistText.Text = displayPolicy.Artist;
        difficultyText.Text = GetDisplayDifficultyName(displayPolicy) ?? string.Empty;

        if (displayPolicy.HasStarRating)
        {
            addStarDifficultyDisplay();

            Debug.Assert(starDisplay is not null);
            Debug.Assert(displayPolicy.Beatmap is not null);

            computeStarRating(displayPolicy.Beatmap);
        }

        if (displayPolicy.Beatmap is not null && beatmaps is not null)
        {
            background_update_task = Scheduler.AddDelayed(
                s => cover.UpdateBackground(beatmaps.GetWorkingBeatmap(s)),
                displayPolicy.Beatmap,
                background_update_debounce);
        }

        // TODO: update play info
        updatePanelState();

        FinishTransforms(true);
    }

    private IBindable<StarDifficulty>? starDifficultyBindable;
    private CancellationTokenSource? starDifficultyCancellationSource;

    private void clearStarDifficultyComputation()
    {
        starDifficultyCancellationSource?.Cancel();
        starDifficultyCancellationSource = null;

        starDifficultyBindable?.UnbindAll();
        starDifficultyBindable = null;
    }

    private void computeStarRating(BeatmapInfo beatmap)
    {
        clearStarDifficultyComputation();

        starDifficultyCancellationSource = new CancellationTokenSource();

        if (difficultyCache is null)
            return;

        starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelectV2.DIFFICULTY_CALCULATION_DEBOUNCE);
        starDifficultyBindable.BindValueChanged(starDifficulty =>
        {
            if (starDisplay is null)
                return;

            starDisplay.Current.Value = starDifficulty.NewValue.Stars;
        }, true);
    }

    protected virtual LocalisableString? GetDisplayDifficultyName(PanelDisplayPolicy displayPolicy)
    {
        if (string.IsNullOrEmpty(displayPolicy.DifficultyName))
            return null;

        string mapper = displayPolicy.Mapper ?? "Unknown";

        return LocalisableString.Format("{0} // {1}", displayPolicy.DifficultyName, mapper);
    }

    protected virtual PanelDisplayPolicy CreateDisplayPolicy(object model)
    {
        switch (model)
        {
            case GroupedBeatmap groupedBeatmap:
                return CreateDisplayPolicy(groupedBeatmap.Beatmap);

            case GroupedBeatmapSet groupedBeatmapSet:
                return CreateDisplayPolicy(groupedBeatmapSet.BeatmapSet);

            default:
                throw new InvalidOperationException($"Display policy for model of type {model.GetType()} is not supported.");
        }
    }

    protected virtual PanelDisplayPolicy CreateDisplayPolicy(BeatmapInfo beatmapInfo)
    {
        var metadata = beatmapInfo.Metadata;

        return new PanelDisplayPolicy(
            new RomanisableString(metadata.TitleUnicode, metadata.Title),
            new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
            beatmapInfo.DifficultyName,
            metadata.Author.Username,
            beatmapInfo,
            true
        );
    }

    protected virtual PanelDisplayPolicy CreateDisplayPolicy(BeatmapSetInfo beatmapSetInfo)
    {
        var metadata = beatmapSetInfo.Metadata;

        return new PanelDisplayPolicy(
            new RomanisableString(metadata.TitleUnicode, metadata.Title),
            new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
            null,
            null,
            beatmapSetInfo.Beatmaps.MinBy(b => b.OnlineID),
            false
        );
    }

    public record PanelDisplayPolicy(RomanisableString Title, RomanisableString Artist, string? DifficultyName, string? Mapper, BeatmapInfo? Beatmap, bool HasStarRating);

    private partial class PanelBeatmapCoverContainer : Container
    {
        private WorkingBeatmap? working;

        public void ClearBackground()
        {
            Clear(true);
            loadCancellationSource?.Cancel();
            working = null;
        }

        private CancellationTokenSource? loadCancellationSource;

        public void UpdateBackground(WorkingBeatmap working)
        {
            // same background, no need to update
            if ((this.working is not null || working is not null) &&
                // SongSelectV2 use this simple way to determine if using the same background
                (getBackgroundFileHash(this.working) == getBackgroundFileHash(working)))
                return;

            this.working = working;

            ClearBackground();

            if (working is null)
                return;

            loadCancellationSource = new CancellationTokenSource();

            LoadComponentAsync(new BeatmapCoverSprite(working)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
                RelativeSizeAxes = Axes.Both,
                Size = Vector2.One,
            }, s =>
            {
                AddInternal(s);
                s.FadeInFromZero(400);
            }, loadCancellationSource.Token);
        }

        private static string? getBackgroundFileHash(WorkingBeatmap? working)
        {
            return working?.BeatmapSetInfo.GetFile(working.Metadata.BackgroundFile)?.File.Hash;
        }

        private partial class BeatmapCoverSprite : Sprite
        {
            private WorkingBeatmap? working;

            public BeatmapCoverSprite(WorkingBeatmap working)
            {
                this.working = working;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (working is not null)
                    Texture = working.GetBackground();
            }
        }
    }
}
