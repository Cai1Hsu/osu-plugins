using osu.Framework;
using osu.Game.Rulesets.PluginsLoader.Tests;
using osu.Game.Tests;

using (var host = Host.GetSuitableDesktopHost(TestOsuGame.GameName, new HostOptions()))
{
    host.Run(new TestOsuGame());
}
