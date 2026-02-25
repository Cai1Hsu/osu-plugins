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
using osuTK.Graphics;
using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;

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

    private unsafe partial class MenuScreen : LazerMenu
    {
        // hide toolbar when entering/returning to menu, as it doesn't fit well with our menu.
        public override bool HideOverlaysOnEnter => true;

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
                SeasonalUIConfig.ENABLED ? new SeasonalMenuSideFlashes() : new LegacyMenuSideFlashes(),
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

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        private static readonly delegate* managed<OsuScreen, ScreenTransitionEvent, void> OsuScreen_OnEntering =
            (delegate* managed<OsuScreen, ScreenTransitionEvent, void>)resolveLifecyclePointer(nameof(OsuScreen.OnEntering), typeof(ScreenTransitionEvent));

        public override void OnEntering(ScreenTransitionEvent e)
        {
            // see https://stackoverflow.com/a/32562464
            // calling base of base without invoking base.
            // OsuScreen's implementations manages things like music, background, etc. which we want to keep working.
            // But we don't want LazerMenu's OnEntering to run, as it will mess up our menu. So we call OsuScreen's OnEntering directly.
            OsuScreen_OnEntering(this, e);

            lazerLogo?.Hide();
            createNewMenu();
        }

        private static readonly delegate* managed<OsuScreen, ScreenTransitionEvent, void> OsuScreen_OnResuming =
            (delegate* managed<OsuScreen, ScreenTransitionEvent, void>)resolveLifecyclePointer(nameof(OsuScreen.OnResuming), typeof(ScreenTransitionEvent));
        public override void OnResuming(ScreenTransitionEvent e)
        {
            OsuScreen_OnResuming(this, e);

            ApplyToBackground(b => (b as BackgroundScreenDefault)?.Next());

            musicController.EnsurePlayingSomething();

            // ButtonSystem.FadeButtonsExcept breaks layout, so recreate the menu instead of just resuming it.
            lazerLogo?.Hide();
            createNewMenu();
        }

        private static readonly delegate* managed<OsuScreen, ScreenTransitionEvent, void> OsuScreen_OnSuspending =
            (delegate* managed<OsuScreen, ScreenTransitionEvent, void>)resolveLifecyclePointer(nameof(OsuScreen.OnSuspending), typeof(ScreenTransitionEvent));

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            OsuScreen_OnSuspending(this, e);
            lazerLogo?.FinishTransforms(true);
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

        private static void* resolveLifecyclePointer(string methodName, params Type[] parameterTypes)
        {
            var method = typeof(OsuScreen).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            if (method is null)
                throw new MissingMethodException(typeof(OsuScreen).FullName, methodName);

            return (void*)method.MethodHandle.GetFunctionPointer();
        }
    }

    private partial class LegacyMenuSideFlashes : MenuSideFlashes
    {
        protected override Color4 GetBaseColour() => Colour4.White;
    }
}