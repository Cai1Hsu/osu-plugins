using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Plugin.LegacyExperience;

public partial class LegacyDialog : DrawSizePreservingFillContainer
{
    public new FillFlowContainer Content { get; } = null!;
    public OsuTextFlowContainer TitleText { get; } = null!;

    public LegacyDialog()
    {
        // I didn't see DrawSizePreservingFillContainer works, but still keep it.
        TargetDrawSize = new Vector2(640, 480);

        RelativeSizeAxes = Axes.Both;
        AddRangeInternal(new Drawable[]
        {
            new Box
            {
                Name = "Background",
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.Black.Opacity(235f / 255f),
            },
            Content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    TitleText = new OsuTextFlowContainer(textCreationParameter)
                    {
                        Name = "Title",
                        Position = new Vector2(2),
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Margin = new MarginPadding
                        {
                            Right = 2
                        }
                    },
                }
            }
        });
    }

    public override void Show()
    {
        this.FadeInFromZero(300);
    }

    public override void Hide()
    {
        this.FadeOut(120);
    }

    public void Close()
    {
        this.FadeOut(120)
            .Expire();
    }

    private static void textCreationParameter(SpriteText t)
    {
        // FIXME: CJK characters look smaller than they should be.
        t.Font = OsuFont.Default.With(size: 24 * LegacyExperiencePlugin.StableRatio);
    }
}
