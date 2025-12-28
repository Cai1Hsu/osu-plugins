using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
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
        var screenStack = GetScreenStack(game);

        static void addOverlayToMainMenu(MainMenu mainMenu)
        {
            // TODO: Bind Enabled property
            mainMenu.AddInternal(new MainMenuPlayerOverlay());
            Logger.Log($"{nameof(MainMenuPlayerOverlay)} added to {nameof(MainMenu)}.");
        }

        void newScreenArrives(IScreen _, IScreen newScreen)
        {
            if (newScreen is not MainMenu mainMenu)
                return;

            addOverlayToMainMenu(mainMenu);

            screenStack.ScreenPushed -= newScreenArrives;
            screenStack.ScreenExited -= newScreenArrives;
        }

        // ensure we are on the update thread to keep events serialized.
        scheduler.Add(() =>
        {
            if (screenStack.CurrentScreen is MainMenu mainMenu)
            {
                addOverlayToMainMenu(mainMenu);
            }
            else
            {
                screenStack.ScreenPushed += newScreenArrives;
                screenStack.ScreenExited += newScreenArrives;
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ScreenStack")]
    private static extern ref OsuScreenStack GetScreenStack(OsuGame game);
}
