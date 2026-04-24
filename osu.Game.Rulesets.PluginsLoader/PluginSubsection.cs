// copied from osu.Game.Overlays.Settings.Sections.InputSubsection

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Plugins;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.PluginsLoader;

public partial class PluginSubsection : SettingsSubsection
{
    protected override LocalisableString Header => Plugin.DisplayName ?? Plugin.GetType().Name;
    protected readonly OsuPlugin Plugin;

    public readonly BindableBool Enabled = new BindableBool();

    public PluginSubsection(OsuPlugin plugin)
    {
        this.Plugin = plugin;
        Enabled.BindTo(plugin.Enabled);
    }

    private Drawable drawableHeader = null!;
    protected override Drawable CreateHeader() => drawableHeader = new ToggleHeader(Header, Plugin.Description, Enabled);

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;

        Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);
        Margin = new MarginPadding { Horizontal = SettingsSection.ITEM_SPACING_V2 };

        if (Plugin.CreateSettingsControls() is { } controls)
        {
            AddRange(controls);
            return;
        }

        foreach (var (attr, prop) in Plugin.GetOrderedSettingsSourceProperties())
        {
            var bindable = prop.GetValue(Plugin) as IBindable;

            if (bindable is null)
                continue;

            var control = createControl(bindable, attr);
            if (control is null)
                continue;

            Add(new SettingsItemV2(control));
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        Enabled.BindValueChanged(updateEnabledState, true);

        FlowContent.Masking = true;
    }

    private void updateEnabledState(ValueChangedEvent<bool> state)
    {
        // set negative bottom margin to not have too much vertical gap between disabled input subsections.
        bool negativeBottomMargin = !Enabled.Value || FlowContent.Count == 0;
        drawableHeader.TransformTo(nameof(Margin), new MarginPadding { Bottom = negativeBottomMargin ? -VERTICAL_PADDING : 0 }, 300, Easing.OutQuint);

        // Avoid crashes from toggling `AutoSizeAxes` while active `AutoSizeDuration` transforms are still running.
        // This is probably a framework bug.
        FlowContent.ClearTransforms();

        if (!Enabled.Value)
        {
            FlowContent.AutoSizeAxes = Axes.None;
            FlowContent.ResizeHeightTo(0, 300, Easing.OutQuint);
        }
        else
        {
            // enable auto size transform momentarily for smooth pop in animation, and disable it right after the transform is added.
            // we don't want this specification to apply when a dropdown in the input settings is being open, it causes too slow animation.
            // (try removing the schedule below then watch a settings dropdown menu opening animation).
            FlowContent.AutoSizeDuration = state.NewValue == state.OldValue ? 0 : 300;
            FlowContent.AutoSizeEasing = Easing.OutQuint;
            FlowContent.AutoSizeAxes = Axes.Y;

            ScheduleAfterChildren(() => FlowContent.AutoSizeDuration = 0);
        }
    }

    private IFormControl? createControl(IBindable bindable, SettingSourceAttribute attr)
    {
        switch (bindable)
        {
            case BindableNumber<float> bNumber:
                return new FormSliderBar<float>
                {
                    Caption = attr.Label,
                    HintText = attr.Description,
                    Current = bNumber,
                    KeyboardStep = bNumber.Precision,
                };

            case BindableNumber<double> bNumber:
                return new FormSliderBar<double>
                {
                    Caption = attr.Label,
                    HintText = attr.Description,
                    Current = bNumber,
                    KeyboardStep = (float)bNumber.Precision,
                };

            case BindableNumber<int> bNumber:
                return new FormSliderBar<int>
                {
                    Caption = attr.Label,
                    HintText = attr.Description,
                    Current = bNumber,
                    KeyboardStep = bNumber.Precision,
                };

            case Bindable<bool> bBool:
                return new FormCheckBox
                {
                    Caption = attr.Label,
                    HintText = attr.Description,
                    Current = bBool
                };

            case Bindable<string> bString:
                return new FormTextBox
                {
                    Caption = attr.Label,
                    HintText = attr.Description,
                    Current = bString
                };

            case IBindable:
                var dropdownType = typeof(FormDropdown<>).MakeGenericType(bindable.GetType().GetGenericArguments()[0]);
                var dropdown = (Drawable)Activator.CreateInstance(dropdownType)!;

                dropdownType.GetProperty(nameof(FormDropdown<>.Caption))?.SetValue(dropdown, attr.Label);
                dropdownType.GetProperty(nameof(FormDropdown<>.HintText))?.SetValue(dropdown, attr.Description);
                dropdownType.GetProperty(nameof(FormDropdown<>.Current))?.SetValue(dropdown, bindable);

                return (IFormControl)dropdown;

            default:
                throw new InvalidOperationException($"{nameof(SettingSourceAttribute)} was attached to an unsupported type ({bindable.GetType().ReadableName()}).");
        }
    }

    private partial class ToggleHeader : CompositeDrawable
    {
        private readonly LocalisableString title;
        private readonly string? description;

        private readonly Bindable<bool> enabled;
        public readonly bool Toggleable = true;

        public ToggleHeader(LocalisableString title, string? description, Bindable<bool> enabled)
        {
            this.title = title;
            this.description = description;
            this.enabled = enabled;

            Padding = SettingsPanel.CONTENT_PADDING;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        private SwitchButton switchButton = null!;
        private OsuSpriteText headerText = null!;
        private OsuTextFlowContainer? descriptionText = null;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                switchButton = new SwitchButton
                {
                    ExpandOnCurrent = false,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Width = 15,
                    Height = 22,
                },
                headerText = new OsuSpriteText
                {
                    Text = title,
                    Font = OsuFont.Style.Heading2,
                    Margin = new MarginPadding { Vertical = 12 },
                    X = 18,
                    Y = -1,
                },
                new HoverSounds(),
            };

            AddInternal(description is null ? Empty() : descriptionText = new OsuTextFlowContainer(d =>
            {
                d.Font = OsuFont.Style.Body;
            })
            {
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding { Vertical = 12 },
                Text = description,
                X = 18,
                Y = headerText.DrawHeight - 1,
            });
        }

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            switchButton.Current.ValueChanged += v => enabled.Value = v.NewValue;

            enabled.BindValueChanged(v =>
            {
                switchButton.Current.Disabled = false;
                switchButton.Current.Value = v.NewValue;
                switchButton.Current.Disabled = !Toggleable;

                updateDisplay();
            }, true);
        }


        protected override bool OnHover(HoverEvent e)
        {
            updateDisplay();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateDisplay();
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (Toggleable)
            {
                enabled.Value = !enabled.Value;
                switchButton.PlaySample(enabled.Value);
            }

            updateDisplay();
            return true;
        }

        private void updateDisplay()
        {
            // default, toggled on (or not toggleable)
            Color4 col = colours.Content1;

            if (!enabled.Value)
                col = IsHovered ? colours.Light1 : colours.Foreground1;

            headerText.FadeColour(col, 300, Easing.OutQuint);
            descriptionText?.FadeColour(col, 300, Easing.OutQuint);
        }
    }
}
