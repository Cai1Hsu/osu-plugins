using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Plugin.LegacyExperience.Graphics;
using osuTK;

namespace osu.Plugin.LegacyExperience;

public partial class LegacyTooltip : VisibilityContainer, ITooltip<LocalisableString>
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

    protected override void PopIn()
    {
        // stable actually has a fade in animation of 200ms for the first display of the tooltip.
        // when moving from one tooltip target to another quickly, the tooltip will just change its content and position without fading out and in again.
        // if we apply fade in for every content change, it will look very weird when moving the mouse across multiple tooltip targets quickly.
        // since we can't determine whether the tooltip is being displayed for the first time or not, we just fade in immedately without animation, which looks better in general.
        this.FadeIn();
    }

    protected override void PopOut()
    {
        this.FadeOut(200);
    }

    private partial class TooltipTextFlowContainer : OsuTextFlowContainer
    {
        // we don't use FontText here since CJK scaling is not needed for tooltip.
        protected override SpriteText CreateSpriteText() => new OsuSpriteText()
        {
            Shadow = false,
            AllowMultiline = false,
            // to make it closer to stable.
            UseFullGlyphHeight = false,
            // it seems that stable uses light font weight, but this looks bad for latin characters, so we use regular weight instead.
            Font = LegacyFont.Default.With(size: 11),
        };
    }
}
