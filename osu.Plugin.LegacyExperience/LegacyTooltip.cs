using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Plugin.LegacyExperience;

public partial class LegacyTooltip : CompositeDrawable, ITooltip<LocalisableString>
{
    public void Move(Vector2 pos) => Position = pos;

    public void SetContent(LocalisableString content) => TextFlow.Text = content;

    public TextFlowContainer TextFlow { get; private set; } = null!;

    public LegacyTooltip()
    {
        CornerRadius = 4; // seems to be correct without stable ratio.
        AutoSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Colour4(10, 10, 10, 230),
            },
            TextFlow = new TooltipTextFlowContainer
            {
                Colour = Colour4.White,
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding(2),
            },
        };

        Masking = true;
        BorderColour = new Colour4(80, 80, 80, 255);
        BorderThickness = 1f;
    }

    private partial class TooltipTextFlowContainer : OsuTextFlowContainer
    {
        protected override SpriteText CreateSpriteText() => new OsuSpriteText()
        {
            Shadow = false,
            AllowMultiline = false,
            // to make it closer to stable.
            UseFullGlyphHeight = false,
            // it seems that stable uses light font weight, but this looks bad for latin characters, so we use regular weight instead.
            Font = OsuFont.Default.With(size: 11 * LegacyExperiencePlugin.StableRatio),
        };
    }
}
