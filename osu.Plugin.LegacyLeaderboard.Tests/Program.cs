using osu.Framework;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.IO.Stores;
using osu.Game.Plugins.Legacy;
using osu.Game.Tests;

var store =  new DllResourceStore(osu.Game.Plugins.Legacy.LegacyResources.ResourceAssembly);

// var store = osu.Game.Plugins.Legacy.LegacyResources.CreateLegacyUIResourceStore();
// store.GetAvailableResources().ForEach(Console.WriteLine);

// new FontStore(.)

using (var host = Host.GetSuitableDesktopHost("osu", new HostOptions()))
{
    var game = new OsuTestBrowser();
    game.EnsureLegacyResources();
    host.Run(game);
}
