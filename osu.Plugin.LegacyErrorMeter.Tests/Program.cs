using osu.Framework;
using osu.Game.Tests;

using (var host = Host.GetSuitableDesktopHost("osu", new HostOptions()))
{
    host.Run(new OsuTestBrowser());
}
