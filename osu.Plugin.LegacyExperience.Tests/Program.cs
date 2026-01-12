using osu.Framework;
using osu.Plugin.LegacyExperience;
using osu.Game.Tests;

using (var host = Host.GetSuitableDesktopHost("osu", new HostOptions()))
{
    var game = new OsuTestBrowser();
    game.EnsureLegacyResources();
    host.Run(game);
}
