using System.Runtime.CompilerServices;
using NUnit.Framework;
using NUnit.Framework.Internal;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Plugins;

namespace osu.Game.Rulesets.PluginsLoader.Tests;

[TestFixture]
public partial class TestPluginConfigManager
{
    [Test]
    public void TestConfigPersists()
    {
        Storage storage = null!;

        using (var host = new TestHost(bypassCleanup: true))
        {
            host.Run(new TestOsuGameBase(o =>
            {
                storage = host.Storage;

                new PluginConfigManager(storage, new TestPlugin()
                {
                    Enabled = { Value = false },
                    TestSetting = { Value = 42 },
                }.Yield().ToArray())
                .Dispose();
            }));
        }

        Assert.That(storage.Exists(PluginConfigManager.ConfigFile));

        var plugin = new TestPlugin();

        using (var host = new TestHost())
        {
            host.Run(new TestOsuGameBase(o =>
            {
                storage = host.Storage;

                new PluginConfigManager(storage, plugin.Yield().ToArray()).Dispose();
            }));
        }

        Assert.That(plugin.Enabled.Value, Is.False);
        Assert.That(plugin.TestSetting.Value, Is.EqualTo(42));
    }

    [Test]
    public void TestChangingSettingSaves()
    {
        var plugin = new TestPlugin();
        var configManager = new TestConfigManager(plugin.Yield().ToArray());

        int lastSaveCount = configManager.SaveCount;

        void assertSaved()
        {
            Assert.That(configManager.SaveEvent.WaitOne(10000), Is.True, "Expected a save operation to be performed within the timeout.");
            Assert.That(configManager.SaveCount > lastSaveCount, Is.True, "Expected a save operation to be performed.");
            lastSaveCount = configManager.SaveCount;
        }

        plugin.Enabled.Value = !plugin.Enabled.Value;
        assertSaved();

        for (int i = 0; i < 5; i++)
        {
            plugin.TestSetting.Value++;
        }
        assertSaved();
    }

    private partial class TestOsuGameBase : OsuGameBase
    {
        private Action<OsuGameBase> action = null!;

        public TestOsuGameBase(Action<OsuGameBase> action)
        {
            this.action = action;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            action(this);
        }

        protected override void Update()
        {
            base.Update();
            Exit();
        }
    }

    private partial class TestHost : TestRunHeadlessGameHost
    {
        public TestHost([CallerMemberName] string name = "", bool bypassCleanup = false)
            : base(name, null, bypassCleanup)
        {
        }
    }

    private partial class TestPlugin : OsuPlugin
    {
        [SettingSource("Test Setting")]
        public Bindable<int> TestSetting { get; } = new Bindable<int>();
    }

    private partial class TestConfigManager : PluginConfigManager
    {
        public int SaveCount { get; private set; }
        public readonly AutoResetEvent SaveEvent = new AutoResetEvent(false);

        public TestConfigManager(OsuPlugin[] plugins)
            : base(null!, plugins)
        {
        }

        protected override void PerformLoad()
        {
        }

        protected override bool PerformSave()
        {
            SaveCount++;
            SaveEvent.Set();
            return true;
        }
    }
}
