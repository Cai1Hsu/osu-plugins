using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Plugin.LegacyExperience.Mods;

partial class LegacyModSelection
{
    public static readonly Vector2 CellSize = new Vector2(66f, 60f) * LegacyExperiencePlugin.StableRatio;

    public partial class SelectionGroup : Container
    {
        public OsuSpriteText Label { get; }

        public LegacyModType GroupType { get; }

        public FillFlowContainer Mods { get; }

        public SelectionGroup(LegacyModType groupType)
        {
            GroupType = groupType;
            RelativeSizeAxes = Axes.X;
            Height = CellSize.Y;

            Name = $"{GroupType} group";

            Children = new Drawable[]
            {
                Label = new OsuSpriteText
                {
                    Position = new Vector2(20f, 13f) * LegacyExperiencePlugin.StableRatio,
                    Font = OsuFont.Default.With(size: 24f * LegacyExperiencePlugin.StableRatio),
                },
                // FIXME:
                // we are depending on LegacyModSwitch's constant size here, which is not ideal.
                // GridContainer must set column dimensions manually, we use FillFlowContainer here just for simplicity.
                Mods = new FillFlowContainer
                {
                    Margin = new MarginPadding
                    {
                        Left = 240 * LegacyExperiencePlugin.StableRatio - (CellSize.X / 2),
                    },
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                },
            };
        }
    }
}