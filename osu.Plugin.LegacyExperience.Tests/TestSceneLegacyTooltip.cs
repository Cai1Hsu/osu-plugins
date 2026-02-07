using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Plugin.LegacyExperience.Tests;

public partial class TestSceneLegacyTooltip : OsuTestScene
{
    private Container content = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(content = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create tooltip", () =>
        {
            content.Clear();

            LegacyTooltip displayTooltip;
            TooltipFeedbackTextBox textBox;

            string lines = string.Empty;

            content.Add(new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    displayTooltip = new LegacyTooltip
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        TextFlow =
                        {
                            Text = "Legacy Experience Tooltip",
                        },
                        State = { Value = Visibility.Visible },
                    },
                    textBox = new TooltipFeedbackTextBox
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(400, 30),
                        PlaceholderText = "Set tooltip text here. Hover the text box to see the tooltip.",
                    },
                    new SettingsButton
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "Add new line",
                        Action = AddNewLine,
                    },
                    new SettingsButton
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "Clear tooltip",
                        Action = () =>
                        {
                            // tooltip not get cleared when there are multiple lines.
                            // whatever, this is just for visual test.
                            lines = string.Empty;
                            textBox.Text = string.Empty;
                        },
                    }
                }
            });

            textBox.OnCommitted = AddNewLine;

            void AddNewLine()
            {
                lines += $"{textBox.Text}\n";
                textBox.Text = string.Empty;
            }

            textBox.Current.BindValueChanged(t => displayTooltip.TextFlow.Text = $"{lines}{t.NewValue}");
        });
    }

    private partial class TooltipFeedbackTextBox : OsuTextBox, IHasLegacyTooltip
    {
        LocalisableString IHasLegacyTooltip.TooltipText => Text;

        public Action? OnCommitted { get; set; }

        protected override void OnTextCommitted(bool textChanged) => OnCommitted?.Invoke();
    }
}
