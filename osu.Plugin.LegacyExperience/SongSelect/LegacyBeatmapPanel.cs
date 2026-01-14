using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyBeatmapPanel : LegacyPanel
{
    private OsuSpriteText titleText = null!;
    private OsuSpriteText artistText = null!;
    private OsuSpriteText difficultyText = null!;
    private StarDifficultyDisplay starDisplay = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(titleText = new OsuSpriteText()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = OsuFont.GetFont(size: 16f * LegacyExperiencePlugin.StableRatio),
            Position = new Vector2(75f, -17f) * LegacyExperiencePlugin.StableRatio,
            AllowMultiline = false,
            Colour = PanelColors.InactiveText,
            // Text = "Beatmap Title",
        });
        AddInternal(artistText = new OsuSpriteText()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = OsuFont.GetFont(size: 12f * LegacyExperiencePlugin.StableRatio),
            Position = new Vector2(76f, -7f) * LegacyExperiencePlugin.StableRatio,
            AllowMultiline = false,
            Colour = PanelColors.InactiveText,
            // Text = "Artist // Mapper",
        });
        AddInternal(difficultyText = new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = OsuFont.GetFont(size: 12f * LegacyExperiencePlugin.StableRatio, weight: FontWeight.Bold),
            Position = new Vector2(76f, 4f) * LegacyExperiencePlugin.StableRatio,
            AllowMultiline = false,
            Colour = PanelColors.InactiveText,
            // Text = "Difficulty Name",
        });

        // TODO: pool it, beapmap sets panel don't need it
        AddInternal(starDisplay = new StarDifficultyDisplay
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.TopLeft,
            // stable uses 7 as Y offset.
            // We tweaked it to 12 for better visual alignment in lazer
            Position = new Vector2(75f, 12f) * LegacyExperiencePlugin.StableRatio,
        });
    }
}
