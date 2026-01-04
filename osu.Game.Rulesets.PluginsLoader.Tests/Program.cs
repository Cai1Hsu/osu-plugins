using osu.Framework;
using osu.Game.Tests;

const string GameName = @"osu-development";

using (var host = Host.GetSuitableDesktopHost(GameName, new HostOptions()))
{
    host.Run(new OsuTestBrowser());
}
