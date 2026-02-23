using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game;
using osu.Game.Overlays;
using osu.Game.Overlays.Volume;
using osu.Game.Screens.Menu;
using osu.Game.Seasonal;
using osu.Game.Plugins;
using MainMenu = osu.Plugin.LegacyExperience.Screens.Menu.MainMenu;
using LazerMenu = osu.Game.Screens.Menu.MainMenu;

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

    private partial class MenuScreen : LazerMenu
    {
        public MenuScreen()
        {
            BackButtonVisibility.Value = false;
        }

        private Container menuContainer = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                SeasonalUIConfig.ENABLED ? new MainMenuSeasonalLighting() : Empty(),
                SeasonalUIConfig.ENABLED ? new SeasonalMenuSideFlashes() : new MenuSideFlashes(),
                menuContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                new GlobalScrollAdjustsVolume(),
                SeasonalUIConfig.ENABLED ? Empty() : new KiaiMenuFountains(),
            };
        }

        [Resolved]
        private OsuLogo? lazerLogo { get; set; }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            lazerLogo?.Hide();
            createNewMenu();
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            // ButtonSystem.FadeButtonsExcept breaks layout, so recreate the menu instead of just resuming it.
            lazerLogo?.Hide();
            createNewMenu();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
        }

        private void createNewMenu()
        {
            // FIXME: we may want to recreate button system only.
            menuContainer.Child = new MainMenu
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