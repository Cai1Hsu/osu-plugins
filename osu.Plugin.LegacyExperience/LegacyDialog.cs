using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Plugin.LegacyExperience;

public partial class LegacyDialog : DrawSizePreservingFillContainer
{
    public new FillFlowContainer Content { get; } = null!;
    public OsuTextFlowContainer TitleText { get; } = null!;

    private FillFlowContainer<OptionButtonContainer> optionsContainer = null!;

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
            new FillFlowContainer
            {
                Name = "Layout",
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    Content = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
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
                    },
                    optionsContainer = new FillFlowContainer<OptionButtonContainer>
                    {
                        Name = "Options",
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Direction = FillDirection.Vertical,
                    }
                }
            },
        });
    }

    protected void AddOption(LocalisableString text, Action<LegacyButton>? configure = null)
    {
        if (LoadState < LoadState.Loaded)
            throw new InvalidOperationException($"Add options in {nameof(LoadComplete)} or later.");

        var labelText = LocalisableString.Interpolate($"{optionsContainer.Count + 1}. {text}");

        var button = new LegacyButton(labelText, new Vector2(460, 40))
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
        configure?.Invoke(button);

        optionsContainer.Add(new OptionButtonContainer(button)
        {
            RelativeSizeAxes = Axes.X,
            Height = 50 * LegacyExperiencePlugin.StableRatio,
        });

        if (IsPresent)
        {
            button.FadeInFromZero(140);
        }
        else
        {
            const float initialOffset = 40f * LegacyExperiencePlugin.StableRatio;

            int delay = optionsContainer.Count * 60;
            bool isOdd = optionsContainer.Count % 2 == 1;

            button.MoveToX(isOdd ? -initialOffset : initialOffset)
                .FadeOut()
                .Then()
                .Delay(delay)
                .FadeInFromZero(800, Easing.Out)
                .MoveToX(0, 800, Easing.OutBounce);
        }
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

    private partial class OptionButtonContainer : CompositeDrawable
    {
        public readonly LegacyButton Button;

        public OptionButtonContainer(LegacyButton button)
        {
            InternalChild = Button = button;
        }
    }
}
