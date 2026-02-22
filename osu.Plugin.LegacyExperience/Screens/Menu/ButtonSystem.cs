using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Input;
using osuTK;
using osuTK.Input;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class ButtonSystem : Container
{
    private readonly Bindable<ButtonSystemState> state = new Bindable<ButtonSystemState>();
    public IBindable<ButtonSystemState> State => state;

    private Container<MenuButton> mainButtons = null!;
    private Container<MenuButton> playButtons = null!;

    public Action? OnPlayClick { get; set; }
    public Action? OnEditClick { get; set; }
    public Action? OnOptionsClick { get; set; }
    public Action? OnExitClick { get; set; }
    public Action? OnFreeplayClick { get; set; }
    public Action? OnMultiplayerClick { get; set; }
    public Action? OnBackClick { get; set; }

    private MenuButton playButton = null!;
    private MenuButton editButton = null!;
    private MenuButton optionsButton = null!;
    private MenuButton soloButton = null!;
    private MenuButton multiButton = null!;
    private MenuButton backButton = null!;

    private Container maskingContainer = null!;
    private Container buttonsContainer = null!;
    private OsuLogo logo = null!;

    private readonly IBindable<bool> isIdle = new BindableBool();

    public Bindable<bool> ParallaxEnabled => parallaxContainer.ParallaxEnabled;

    private MenuParallaxContainer parallaxContainer;

    protected override Container<Drawable> Content => parallaxContainer; 

    public ButtonSystem()
    {
        AddInternal(parallaxContainer = new MenuParallaxContainer()
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [BackgroundDependencyLoader]
    private void load(IdleTracker? idleTracker)
    {
        RelativeSizeAxes = Axes.Both;

        // i donno if we should manage logo here but since it's not used anywhere else
        // and it has to interact with the buttons a lot it makes sense to just put it here
        logo = new OsuLogo
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Action = logoClicked,
        };

        Children = new Drawable[]
        {
            logo.CreateEffectsProxy(),
            maskingContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Masking = true,
                Child = buttonsContainer = new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        mainButtons = new Container<MenuButton>
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            ChildrenEnumerable = new MenuButton[]
                            {
                                playButton = new MenuButton()
                                {
                                    Name = "play",
                                    Position = new Vector2(-100f, -125f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => updateState(ButtonSystemState.Play),
                                }.With(d => d.Action += () => OnPlayClick?.Invoke()),
                                editButton = new MenuButton()
                                {
                                    Name = "edit",
                                    Position = new Vector2(-100f, -60f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => OnEditClick?.Invoke(),
                                },
                                optionsButton = new MenuButton()
                                {
                                    Name = "options",
                                    Position = new Vector2(-100f, 5f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => OnOptionsClick?.Invoke(),
                                },
                                new MenuButton()
                                {
                                    Name = "exit",
                                    Position = new Vector2(-100f, 70f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => OnExitClick?.Invoke(),
                                },
                            }.Select(configureButton),
                        },
                        playButtons = new Container<MenuButton>
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            ChildrenEnumerable = new MenuButton[]
                            {
                                soloButton = new MenuButton()
                                {
                                    Name = "freeplay",
                                    Position = new Vector2(-100f, -92.5f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => OnFreeplayClick?.Invoke(),
                                },
                                multiButton = new MenuButton()
                                {
                                    Name = "multiplayer",
                                    Position = new Vector2(-100f, -27.5f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => OnMultiplayerClick?.Invoke(),
                                },
                                backButton = new MenuButton()
                                {
                                    Name = "back",
                                    Position = new Vector2(-100f, 37.5f) * LegacyExperiencePlugin.StableRatio,
                                    Action = () => updateState(ButtonSystemState.Main),
                                }.With(d => d.Action += () => OnBackClick?.Invoke()),
                            }.Select(configureButton),
                        },
                    }
                },
            },
            logo,
        };

        if (idleTracker is not null)
            isIdle.BindTo(idleTracker.IsIdle);

        isIdle.BindValueChanged(idle =>
        {
            if (idle.NewValue)
                updateState(ButtonSystemState.Collapsed);
        }, true);

        updateState(ButtonSystemState.Collapsed);
        FinishTransforms(true);
    }

    protected override void Update()
    {
        base.Update();

        // Align the mask's left edge with the logo's center so that
        // button corners cannot peek out from behind the circular logo.
        // stable actually has this bug but it looks really bad in lazer so here we are.
        maskingContainer.X = logo.X;
        buttonsContainer.X = -logo.X;
    }

    private static MenuButton configureButton(MenuButton button)
    {
        button.Anchor = Anchor.CentreLeft;

        return button;
    }

    private void logoClicked()
    {
        switch (state.Value)
        {
            case ButtonSystemState.Collapsed:
                updateState(ButtonSystemState.Main);
                break;
            case ButtonSystemState.Main:
                playButton.TriggerClick();
                break;
            case ButtonSystemState.Play:
                soloButton.TriggerClick();
                break;
        }
    }

    private void updateState(ButtonSystemState newState)
    {
        ButtonSystemState oldState = state.Value;
        bool isBack = newState < oldState;

        switch (newState)
        {
            case ButtonSystemState.Collapsed:
                logo.MoveToX(0, 2000, Easing.Out);
                hideButtons(mainButtons);
                hideButtons(playButtons);
                break;

            case ButtonSystemState.Main when oldState is not ButtonSystemState.Collapsed:
                switchButtons(mainButtons);
                fadeOutButtons(playButtons);
                break;

            case ButtonSystemState.Play when oldState is not ButtonSystemState.Collapsed:
                switchButtons(playButtons);
                fadeOutButtons(mainButtons);
                break;

            case ButtonSystemState.Main:
                presentButtons(mainButtons);
                break;

            case ButtonSystemState.Play:
                presentButtons(playButtons);
                break;
        }

        if (newState is not ButtonSystemState.Collapsed)
        {
            logo.MoveToX(-120 * LegacyExperiencePlugin.StableRatio, 400, Easing.Out);
        }

        void hideButtons(Container<MenuButton> buttons)
        {
            buttonsContainer.ChangeChildDepth(buttons, 1);

            buttons.FadeOut(500, Easing.None)
                   .MoveToX(-100 * LegacyExperiencePlugin.StableRatio, 2000, Easing.Out);
        }

        void presentButtons(Container<MenuButton> buttons)
        {
            buttonsContainer.ChangeChildDepth(buttons, 0);

            buttons.X = -100f * LegacyExperiencePlugin.StableRatio;
            buttons.FadeIn(350, Easing.None)
                   .MoveToX(0, 400, Easing.Out);
        }

        void switchButtons(Container<MenuButton> buttons)
        {
            buttonsContainer.ChangeChildDepth(buttons, 0);

            buttons.ClearTransforms();

            buttons.X = (isBack ? 15 : -30) * LegacyExperiencePlugin.StableRatio;
            buttons.FadeIn(80)
                   .MoveToX(0, 200, Easing.Out);
        }

        void fadeOutButtons(Container<MenuButton> buttons)
        {
            buttonsContainer.ChangeChildDepth(buttons, 1);

            buttons.ClearTransforms();
            buttons.FadeOut(200);
        }

        state.Value = newState;
    }

    /// <summary>
    /// Fades out all buttons except the one with the specified name and moves them to the left so that they don't peek out from behind the logo.
    /// This is used in screen transitions to create a nice effect of the buttons fading out towards the logo.
    /// </summary>
    /// <param name="name">The name of the button to keep visible.</param>
    public void FadeButtonsExcept(string name)
    {
        var button = buttonsContainer.OfType<Container<MenuButton>>()
                                     .SelectMany(static c => c)
                                     .FirstOrDefault(b => b.Name == name);

        if (button?.Parent is not Container<MenuButton> buttonContainer)
            return;

        if (!buttonContainer.IsPresent)
            return;

        buttonContainer.Remove(button, false);

        buttonContainer.MoveToOffset(new Vector2(-50, 0) * LegacyExperiencePlugin.StableRatio, 100, Easing.None)
                       .FadeOut(100, Easing.None);

        // this method is only used in screen transitions,
        // so breaking the layout here doesn't really matter since the screen will be gone by the time it becomes an issue,
        // and it saves us from having to add a lot of complexity to handle the layout readjustment after removing the button.
        buttonsContainer.Add(button);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        // ignore keybinds with modifiers to avoid interfering with user keybinds (e.g. ctrl+e to open editor)
        // also we don't want to stop idle state via modifier keys since that would be really annoying
        if (e.AltPressed || e.ControlPressed || e.ShiftPressed || e.SuperPressed)
            return base.OnKeyDown(e);
        
        if (state.Value is ButtonSystemState.Collapsed)
            updateState(ButtonSystemState.Main);

        if (state.Value is ButtonSystemState.Play)
        {
            switch (e.Key)
            {
                case Key.Escape:
                case Key.B:
                    backButton.TriggerClick();
                    return true;

                case Key.P:
                    logo.TriggerClick();
                    return true;

                case Key.M:
                    multiButton.TriggerClick();
                    return true;
            }
        }
        // this condition is redudant since the first if will already switch to main state but just in case
        else if (state.Value is ButtonSystemState.Main)
        {
            switch (e.Key)
            {
                case Key.O:
                    optionsButton.TriggerClick();
                    return true;

                case Key.P:
                    logo.TriggerClick();
                    return true;

                case Key.E:
                    editButton.TriggerClick();
                    return true;
            }
        }

        return base.OnKeyDown(e);
    }

    public enum ButtonSystemState
    {
        Collapsed = 0,
        Main = 1,
        Play = 2,
    }

    private partial class MenuParallaxContainer : Container
    {
        public readonly Bindable<bool> ParallaxEnabled = new BindableBool();

        private Container content;

        protected override Container<Drawable> Content => content;

        public MenuParallaxContainer()
        {
            AddInternal(content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            });
        }

        private InputManager inputManager = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager();
        }

        protected override void Update()
        {
            base.Update();

            if (ParallaxEnabled.Value)
            {
                var localPosition = ToLocalSpace(inputManager.CurrentState.Mouse.Position);
                var centerOffset = localPosition - (content.DrawSize / 2);

                content.Position = -centerOffset / 60f;
            }
            else
            {
                content.Position = Vector2.Zero;
            }
        }
    }
}
