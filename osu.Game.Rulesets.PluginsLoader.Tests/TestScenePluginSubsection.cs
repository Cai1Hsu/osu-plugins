using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Game.Tests.Visual;
using osu.Game.Overlays.Settings;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Rulesets.PluginsLoader.Tests;

public partial class TestScenePluginSubsection : OsuTestScene
{
    private TestPluginWithNameAndDescription plugin1 = new TestPluginWithNameAndDescription();
    private TestPluginWithoutNameAndDescription plugin2 = new TestPluginWithoutNameAndDescription();
    private TestPluginWithManySettings plugin3 = new TestPluginWithManySettings();

    private SettingsSection content = null!;

    [Cached]
    [SuppressMessage("CodeQuality", "IDE0052", Justification = "DI usage")]
    private readonly OverlayColourProvider colours = new OverlayColourProvider(OverlayColourScheme.Purple);

    [BackgroundDependencyLoader]
    private void load()
    {
        TestSettingsOverlay settingsOverlay;

        Add(settingsOverlay = new TestSettingsOverlay());
        content = settingsOverlay.PluginManagerSettings;

        AddStep("toggle visibility", () => settingsOverlay.ToggleVisibility());
    }

    [SetUp]
    public void SetUp()
    {
        content.Clear(true);

        content.AddRange(new[]
        {
            new TestSubSection(plugin3),
            new TestSubSection(plugin2),
            new TestSubSection(plugin1),
        });
    }

    [Test]
    public void TestSubsectionCollapsesWhenDisabled()
    {
        TestSubSection? subsection = null;
        OsuPlugin? plugin = null;

        AddStep("find subsection", () =>
        {
            subsection = content.ChildrenOfType<TestSubSection>().FirstOrDefault(s => s.Plugin == plugin3);
            plugin = subsection?.Plugin;
        });

        AddStep("disable plugin", () => plugin?.Enabled.Value = false);
        AddAssert("header is disabled",
            () => subsection?.Enabled.Value is false);
        AddUntilStep("subsection collapsing", () => subsection?.FlowContent.Height is 0);

        AddStep("enable plugin", () => plugin?.Enabled.Value = true);
        AddAssert("subsection is expanded", () => subsection?.Enabled.Value is true);
        AddUntilStep("subsection expanded", () => subsection?.FlowContent.Height > 0);
    }

    [Test]
    public void TestValuesAreSynced()
    {
        TestSubSection? subsection = null;

        AddStep("find subsection", () => subsection = content.ChildrenOfType<TestSubSection>().FirstOrDefault(s => s.Plugin == plugin3));
        AddAssert("not null", () => subsection is not null);

        AddStep("change setting", () =>
        {
            var settingItem = subsection.ChildrenOfType<SettingsItemV2>().First(t => t.Control is FormSliderBar<int>);
            var control = (FormSliderBar<int>)settingItem.Control;

            control.Current.Value = 5;
        });

        AddAssert("setting value changed", () => plugin3.SettingInt.Value is 5);
    }

    private class TestPluginWithNameAndDescription : OsuPlugin
    {
        public override string? DisplayName => "Test Plugin";
        public override string? Description => "A simple test plugin for demonstration purposes.";
    }

    private class TestPluginWithoutNameAndDescription : OsuPlugin
    {
        public override string? DisplayName => null;
        public override string? Description => null;
    }

    private class TestPluginWithManySettings : OsuPlugin
    {
        public override string? DisplayName => "Test Plugin With Many Settings";
        public override string? Description => "A test plugin with many settings for demonstration purposes.";

        [SettingSource(nameof(SettingInt), "An example integer setting.")]
        public BindableInt SettingInt { get; } = new BindableInt(1)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        [SettingSource(nameof(SettingBool), "An example boolean setting.")]
        public BindableBool SettingBool { get; } = new BindableBool(true);

        [SettingSource(nameof(SettingFloat), "An example float setting.")]
        public BindableFloat SettingFloat { get; } = new BindableFloat(0.5f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(nameof(SettingDouble), "An example double setting.")]
        public BindableDouble SettingDouble { get; } = new BindableDouble(0.25)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01,
        };

        [SettingSource(nameof(SettingString), "An example string setting.")]
        public Bindable<string> SettingString { get; } = new Bindable<string>("Test");

        [SettingSource(nameof(SettingEnum), "An example enum setting.")]
        public Bindable<TestEnum> SettingEnum { get; } = new Bindable<TestEnum>(TestEnum.Option1);

        public enum TestEnum
        {
            Option1,
            Option2,
            Option3
        }
    }

    private partial class TestSubSection : PluginSubsection
    {
        public TestSubSection(OsuPlugin plugin)
            : base(plugin)
        {
        }

        public new OsuPlugin Plugin => base.Plugin;

        public new FillFlowContainer FlowContent => base.FlowContent;
    }

    private partial class TestSettingsOverlay : SettingsPanel
    {
        public TestSettingsOverlay() : base(false)
        {
        }

        public readonly PluginsSection PluginManagerSettings = new PluginsSection();

        protected override IEnumerable<SettingsSection> CreateSections() => PluginManagerSettings.Yield();
    }
}
