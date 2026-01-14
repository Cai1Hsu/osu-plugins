using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyBeatmapPanel : LegacyPanel
{
    private OsuSpriteText titleText = null!;
    private OsuSpriteText artistText = null!;
    private OsuSpriteText difficultyText = null!;
    private StarDifficultyDisplay? starDisplay = null!;
    private FillFlowContainer fillFlowContainer = null!;

    private Container coverContainer = null!;
    private Container playInfoContainer = null!;
    private Container beatmapInfoContainer = null!;

    [Resolved]
    private DrawablePool<StarDifficultyDisplay> starDifficultyPool { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(fillFlowContainer = new FillFlowContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
            Direction = FillDirection.Horizontal,
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                // container used to display the cover image
                coverContainer = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 75 * LegacyExperiencePlugin.StableRatio,
                },
                // container used to display play info like
                // - local best rank
                // - ruleset icon if not the selected one
                // Currently unused, but we may add more info later.c
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
        });

        // temporarily add star display here for testing purpose
        addStarDifficultyDisplay();
    }

    private void clearStarDifficultyDisplay()
    {
        starDisplay?.Expire();
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
}
