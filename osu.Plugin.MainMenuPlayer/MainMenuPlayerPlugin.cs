using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Menu;

namespace osu.Plugin.MainMenuPlayer;

public class MainMenuPlayerPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        var game = (OsuGame)gameBase;

        static void addOverlayToMainMenu(Drawable d)
        {
            var mainMenu = (MainMenu)d;

            // TODO: Bind Enabled property
            mainMenu.AddInternal(new MainMenuPlayerOverlay());
            Logger.Log($"{nameof(MainMenuPlayerOverlay)} added to {nameof(MainMenu)}.");
        }

        game.PerformOnceFromScreen((_, screen) =>
        {
            if (screen is not MainMenu mainMenu)
                return;

            mainMenu.InvokeWhenReady(addOverlayToMainMenu);
        }, new[] { typeof(MainMenu) });
    }
}
