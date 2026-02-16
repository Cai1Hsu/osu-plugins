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
using osu.Framework.Layout;
using NUnit.Framework;
using static osu.Plugin.LegacyExperience.Graphics.NativeText;
using System.Runtime.Versioning;

namespace osu.Plugin.LegacyExperience.Tests.Graphics;

public partial class TestSceneNativeText : OsuTestScene
{
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
    private readonly Bindable<Colour4> textColour = new Bindable<Colour4>(Colour4.White);

    private readonly Bindable<string> inputValue = new Bindable<string>("Hello, Legacy Experience!");

    private Container createDependencyContainer(INativeText nativeText) => new DependencyProvidingContainer()
    {
        RelativeSizeAxes = Axes.Both,
        CachedDependencies = new (Type, object)[]
        {
            (typeof(INativeText), nativeText)
        },
        Children = new Drawable[]
        {
            (Drawable)nativeText,
        }
    };

    [Test]
    public void TestImageSharpNativeText() 
        => createTestScene(() => createDependencyContainer(new ImageSharpNativeText()));

    [Test]
    [Platform(Include = "Win")]
    [SupportedOSPlatform("windows")]
    public void TestGdipNativeText()
        => createTestScene(() => createDependencyContainer(new GdipNativeText()));

    private Container contentContainer = null!;

    private void createTestScene(Func<Container> createContent)
    {
        AddStep("setup", () =>
        {
            var content = createContent();
            contentContainer.Child = content;
            content.Add(nativeTextContainer = new NativeTextContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Size = new Vector2(400, 100),
                Margin = new MarginPadding { Top = 100 },
            });

            textRectangleHeight.TriggerChange();
            textRectangleWidth.TriggerChange();
            fontSize.TriggerChange();
            inputValue.TriggerChange();
            textColour.TriggerChange();
        });
    }

    private NativeTextContainer? nativeTextContainer;

    [BackgroundDependencyLoader]
    private void load(OsuGameBase game)
    {
        Add(contentContainer = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });

        Add(new SettingsEnumDropdown<Language>()
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            AlwaysShowSearchBar = true,
            Current = { BindTarget = game.CurrentLanguage },
            LabelText = "Game language",
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
                    Action = () => nativeTextContainer?.ForceRedraw(),
                },
            }
        });

        AddStep("set Black", () => textColour.Value = Colour4.Black);
        AddStep("set White", () => textColour.Value = Colour4.White);
        AddStep("set Red", () => textColour.Value = Colour4.Red);
        AddStep("set Blue", () => textColour.Value = Colour4.Blue);

        AddToggleStep("toggle masking", enabled => nativeTextContainer?.Masking = enabled);

        textRectangleWidth.BindValueChanged(width => nativeTextContainer?.Width = width.NewValue, true);
        textRectangleHeight.BindValueChanged(height => nativeTextContainer?.Height = height.NewValue, true);

        fontSize.BindValueChanged(size => nativeTextContainer?.TextSize = size.NewValue, true);
        inputValue.BindValueChanged(value => nativeTextContainer?.Text = value.NewValue, true);

        textColour.BindValueChanged(colour => nativeTextContainer?.Colour = colour.NewValue, true);
    }

    private partial class NativeTextContainer : Container
    {
        [Resolved]
        private INativeText nativeText { get; set; } = null!;

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

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.DarkGray.Opacity(0.3f),
                },
                textSprite = new Sprite(),
            };
        }

        private Sprite textSprite = null!;

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
            nativeText.CreateText(new TextCreationParameters
            {
                Text = text,
                Size = TextSize,
                RestrictBounds = DrawSize,
                RenderFlags = TextRenderFlags.Render
            }, out var result);

            var texture = result.Texture;

            textSprite.Texture = texture;
            textSprite.Size = texture?.DisplaySize ?? Vector2.Zero;
        }
    }
}
