using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using SongSelectV2 = osu.Game.Screens.SelectV2.SongSelect;

namespace osu.Plugin.StableIntegrationPlugin;

public class StableIntegrationPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        OsuGame game = (OsuGame)gameBase;

        registerFooterButtonHook();
        scheduler.Add(() =>
        {
            game.InjectDependencies(out StableIntegrationManager _, () => new());
        });

        void registerFooterButtonHook()
        {
            ScreenFooter? screenFooter = game.Dependencies.Get<ScreenFooter>();

            if (screenFooter == null)
            {
                CancelActivation("ScreenFooter not found, Stable Integration Plugin will not function.");
                return;
            }

            var footerContent = GetFooterContent(screenFooter);

            void makeButtonAppearFromBottom(ScreenFooterButton button, int index)
            {
                try
                {
                    makeButtonAppearFromBottom(screenFooter, button, index);
                }
                catch
                {
                    const float delay_per_button = 30;

                    // fallback path
                    button.AppearFromBottom(index * delay_per_button);
                }

                [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "makeButtonAppearFromBottom")]
                extern static void makeButtonAppearFromBottom(ScreenFooter footer, ScreenFooterButton button, int index);
            }

            void addButtonToScreenFooter(Drawable screen)
            {
                var songSelect = (SongSelectV2)screen;

                if (!songSelect.IsCurrentScreen())
                    return;

                ScreenFooterButton button = new PlayInStableButton();

                var index = footerContent.Count; // we expect all add operations to be sequential

                button.OnLoadComplete += d => makeButtonAppearFromBottom((ScreenFooterButton)d, index);
                footerContent.Add(button);
            }

            void newScreenArrives(IScreen _, IScreen screen)
            {
                if (screen is not SongSelectV2 songSelect)
                    return;

                if (songSelect.IsLoaded)
                    addButtonToScreenFooter(songSelect);
                else
                    songSelect.OnLoadComplete += addButtonToScreenFooter;
            }

            ScreenStack screenStack = GetScreenStack(game);

            screenStack.ScreenPushed += newScreenArrives;
            screenStack.ScreenExited += newScreenArrives;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "buttonsFlow")]
    private extern static ref FillFlowContainer<ScreenFooterButton> GetFooterContent(ScreenFooter footer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ScreenStack")]
    private static extern ref OsuScreenStack GetScreenStack(OsuGame game);
}
