using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osu.Game.Screens.SelectV2;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.SongSelect;

public abstract partial class LegacyPanelHasBeatmap : LegacyPanel
{
    protected OsuSpriteText titleText = null!;
    protected OsuSpriteText artistText = null!;

    protected PanelBeatmapCoverContainer cover = null!;
    protected Container BeatmapInfoContainer = null!;

    [Resolved]
    private BeatmapManager? beatmaps { get; set; }

    [Resolved]
    protected ISongSelect? songSelect { get; private set; }

    public Bindable<ScoreInfo?> LocalBestScore { get; } = new Bindable<ScoreInfo?>();

    private static readonly Vector2 cover_position = new Vector2(5.2f, 0.25f);
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
                    CreatePlayInfo(),
                    BeatmapInfoContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = CreateBeatmapInfoChildren(),
                    },
                }
            }
        });

        Selected.BindValueChanged(_ => updatePanelState());
        Expanded.BindValueChanged(_ => updatePanelState(), true);
    }

    protected virtual Drawable CreatePlayInfo() => Empty();

    protected virtual Drawable[] CreateBeatmapInfoChildren() => new Drawable[]
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
    };

    protected override void SkinChanged()
    {
        base.SkinChanged();

        Scheduler.Add(updatePanelState);
    }

    private void updatePanelState()
    {
        bool isActivated = Expanded.Value || Selected.Value;
        bool activatedBySet = !isActivated && (Carousel?.IsBeatmapPanelFromExpandedSet(this) ?? false);

        UpdatePanelState(isActivated, activatedBySet);
    }

    protected virtual void UpdatePanelState(bool isActivated, bool activatedBySet)
    {
        // match stable's behavior:
        // panels from expanded set but not selected looks darker than even inactive panels.
        var textColor = isActivated ? PanelColors.ActiveText :
                        activatedBySet ? PanelColors.InactiveTextFaded : PanelColors.InactiveText;

        titleText.Colour = textColor;
        artistText.Colour = textColor;

        var coverColor = isActivated || activatedBySet ? PanelColors.White : PanelColors.InactiveCover;

        cover.FadeColour(coverColor, 300);
    }

    protected override Colour4 GetBackgroundColor()
    {
        if (Selected.Value)
            return PanelColors.White;

        if (Expanded.Value ||
            (Carousel?.IsBeatmapPanelFromExpandedSet(this) ?? false))
            return PanelColors.Blue;

        return LocalBestScore.Value is null
            ? PanelColors.Pink
            : PanelColors.Orange;
    }

    protected override void FreeAfterUse()
    {
        cover.ClearBackground();
        background_update_task?.Cancel();
        background_update_task = null;

        base.FreeAfterUse();
    }

    private const float background_update_debounce = 250;
    private ScheduledDelegate? background_update_task;

    protected override void PrepareForUse()
    {
        base.PrepareForUse();

        Debug.Assert(Item is not null);

        var displayPolicy = CreateDisplayPolicy(Item.Model);

        titleText.Text = displayPolicy.Title;
        artistText.Text = displayPolicy.Artist;

        if (displayPolicy.CoverBeatmap is not null && beatmaps is not null)
        {
            // The debounce is intended to reduce the number of background loading operations
            // when rapidly scrolling through the song select.
            background_update_task = Scheduler.AddDelayed(() =>
            {
                cover.UpdateBackground(beatmaps.GetWorkingBeatmap(displayPolicy.CoverBeatmap));
                background_update_task = null;
            }, background_update_debounce);
        }

        updatePanelState();

        FinishTransforms(true);
    }

    public void FinishBackgroundTask()
    {
        background_update_task?.RunTask();
    }

    protected abstract PanelDisplayPolicy CreateDisplayPolicy(object model);

    public record PanelDisplayPolicy(RomanisableString Title, RomanisableString Artist, BeatmapInfo? CoverBeatmap);

    protected partial class PanelBeatmapCoverContainer : Container
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
            if ((this.working is null && working is null) ||
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
