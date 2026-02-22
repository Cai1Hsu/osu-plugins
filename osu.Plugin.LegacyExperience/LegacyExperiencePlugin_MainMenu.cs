using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Graphics;
using osu.Game;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using MainMenu = osu.Plugin.LegacyExperience.Screens.Menu.MainMenu;
using LazerMenu = osu.Game.Screens.Menu.MainMenu;
using osu.Game.Overlays;

namespace osu.Plugin.LegacyExperience;

partial class LegacyExperiencePlugin
{
    private void hookMainMenu(OsuGame game)
    {
        var screenStack = game.ScreenStack;

        game.PerformOnceFromScreen((oldScreen, newScreen) =>
        {
            if (newScreen is not LazerMenu lazerMenu)
                return;

            lazerMenu.InvokeWhenReady(d => d.Hide());

            game.Scheduler.Add(() =>
            {
                var logoProxy = logoProxy_Field?.GetValue(lazerMenu) as IDisposable;
                logoProxy?.Dispose();

                // we want to kill lazer menu without let it know
                exitFrom_Method?.Invoke(screenStack, new object?[] { null, false, false, null });

                screenStack.Push(new MenuScreen()
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                });
            });
        }, new Type[] { typeof(LazerMenu) });
    }
    
    private static readonly FieldInfo? logoProxy_Field = typeof(LazerMenu)
        .GetField("logoProxy", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo? exitFrom_Method = typeof(ScreenStack)
        .GetMethod("exitFrom", BindingFlags.Instance | BindingFlags.NonPublic);

    private partial class MenuScreen : OsuScreen
    {
        public override bool ShowFooter => false;
        public override bool HideOverlaysOnEnter => true;

        public MenuScreen()
        {
            BackButtonVisibility.Value = false;
        }

        [Resolved]
        private OsuLogo? lazerLogo { get; set; }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            lazerLogo?.Hide();
            createNewMenu();
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            // ButtonSystem.FadeButtonsExcept breaks layout, so recreate the menu instead of just resuming it.
            lazerLogo?.Hide();
            createNewMenu();
        }

        private void createNewMenu()
        {
            InternalChild = new MainMenu
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            };
        }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private bool allowExiting = false;

        private void confirmExit()
        {
            allowExiting = true;
            this.Exit();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // FIXME: exit button is also blocked.
            if (allowExiting || dialogOverlay is null)
                return false;

            dialogOverlay.Push(new ConfirmExitDialog(onConfirm: confirmExit));

            return true;
        }
    }
}