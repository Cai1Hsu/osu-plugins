using osu.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.IO.Stores;
using osu.Plugin.Legacy;
using osu.Game.Tests;

using (var host = Host.GetSuitableDesktopHost("osu", new HostOptions()))
{
    var game = new OsuTestBrowser();
    game.EnsureLegacyResources();
    host.Run(game);
}
