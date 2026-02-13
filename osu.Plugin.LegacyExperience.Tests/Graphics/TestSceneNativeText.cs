using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Tests.Visual;
using osu.Game;
using osu.Framework.Graphics.Containers;
using osu.Plugin.LegacyExperience.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Shapes;
using osuTK;
using static osu.Plugin.LegacyExperience.Graphics.NativeText;
using osu.Framework.Layout;

namespace osu.Plugin.LegacyExperience.Tests.Graphics;

public partial class TestSceneNativeText : OsuTestScene
{
    [Cached]
    private readonly NativeText nativeText = new NativeText();

    private readonly BindableFloat textRectangleWidth = new BindableFloat
    {
        Value = 400,
        MinValue = 50,
        MaxValue = 1000,
    };
    private readonly BindableFloat textRectangleHeight = new BindableFloat
    {
        Value = 100,
        MinValue = 20,
        MaxValue = 500,
    };

    private readonly BindableFloat fontSize = new BindableFloat
    {
        Value = 13,
        MinValue = 5,
        MaxValue = 72,
    };

    private readonly Bindable<string> inputValue = new Bindable<string>("Hello, Legacy Experience!");

    [BackgroundDependencyLoader]
    private void load(OsuGameBase game)
    {
        Add(nativeText);

        Add(new SettingsEnumDropdown<Language>()
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            AlwaysShowSearchBar = true,
            Current = { BindTarget = game.CurrentLanguage },
            LabelText = "Game language",
        });

        NativeTextContainer nativeTextContainer;

        Add(nativeTextContainer = new NativeTextContainer
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Size = new Vector2(400, 100),
            Margin = new MarginPadding { Top = 100 },
        });

        Add(new FillFlowContainer
        {
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            Children = new Drawable[]
            {
                new SettingsSlider<float>()
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    LabelText = "Font Size",
                    Current = { BindTarget = fontSize },
                },
                new SettingsSlider<float>()
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    LabelText = "Text Rectangle Width",
                    Current = { BindTarget = textRectangleWidth },
                },
                new SettingsSlider<float>()
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    LabelText = "Text Rectangle Height",
                    Current = { BindTarget = textRectangleHeight },
                },
                new OsuTextBox
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(300, 40),
                    PlaceholderText = "Enter text to render...",
                    Current = { BindTarget = inputValue },
                },
                new SettingsButton
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "Force Redraw",
                    Action = () => nativeTextContainer.ForceRedraw(),
                },
            }
        });

        textRectangleWidth.BindValueChanged(width => nativeTextContainer.Width = width.NewValue, true);
        textRectangleHeight.BindValueChanged(height => nativeTextContainer.Height = height.NewValue, true);

        fontSize.BindValueChanged(size => nativeTextContainer.TextSize = size.NewValue, true);
        inputValue.BindValueChanged(value => nativeTextContainer.Text = value.NewValue, true);

        AddStep("set Black", () => nativeTextContainer.Colour = Colour4.Black);
        AddStep("set White", () => nativeTextContainer.Colour = Colour4.White);
        AddStep("set Red", () => nativeTextContainer.Colour = Colour4.Red);
        AddStep("set Blue", () => nativeTextContainer.Colour = Colour4.Blue);

        AddToggleStep("toggle masking", enabled => nativeTextContainer.Masking = enabled);
    }

    private partial class NativeTextContainer : Container
    {
        [Resolved]
        private NativeText nativeText { get; set; } = null!;

        private string text = string.Empty;

        public string Text
        {
            get => text;
            set
            {
                if (text == value)
                    return;

                text = value;
                textureLayout.Invalidate();
            }
        }

        private float textSize = 13;
        public float TextSize
        {
            get => textSize;
            set
            {
                if (textSize == value)
                    return;

                textSize = value;
                textureLayout.Invalidate();
            }
        }

        public new Colour4 Colour
        {
            get => textSprite.Colour;
            set => textSprite.Colour = value;
        }

        private LayoutValue textureLayout = new LayoutValue(Invalidation.DrawSize);

        public NativeTextContainer()
        {
            AddLayout(textureLayout);
        }

        private Sprite textSprite = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.DarkGray.Opacity(0.3f),
            });
            AddInternal(textSprite = new Sprite());
        }

        public void ForceRedraw() => textureLayout.Invalidate();

        protected override void Update()
        {
            base.Update();

            if (!textureLayout.IsValid)
            {
                updateText();
                textureLayout.Validate();
            }
        }

        private void updateText()
        {
            var texture = nativeText.CreateText(new TextCreationParameters
            {
                Text = text,
                Size = TextSize,
                RestrictBounds = DrawSize * 1.6f,
                Dpi = 96,
            });
            texture?.ScaleAdjust = 1.6f;

            textSprite.Texture = texture;
            textSprite.Size = texture?.DisplaySize ?? Vector2.Zero;
        }
    }
}
