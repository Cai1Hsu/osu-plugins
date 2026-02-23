using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.SelectV2;
using osu.Plugin.LegacyExperience.Audio;
using osu.Plugin.LegacyExperience.Graphics;
using osu.Plugin.LegacyExperience.Localisations;
using osu.Plugin.LegacyExperience.Online;
using osuTK;
using LazerLogo = osu.Game.Screens.Menu.OsuLogo;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class MainMenu : CompositeDrawable
{
    private ButtonSystem buttonSystem = null!;

    [Cached(typeof(IAmplitudesProvider))]
    private AmplitudesProvider amplitudesProvider = new AmplitudesProvider();

    private Bindable<bool> parallaxEnabled = null!;

    private MenuIcon supporterIcon = null!;
    private MenuIcon batIcon = null!;

    private OsuDirectButton osuDirectButton = null!;

    private Container<LegacyUserPanel> userPanelContainer = null!;

    private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

    private string versionString
    {
        get
        {
            var version = typeof(OsuGameBase).Assembly.GetName().Version ?? new Version();

            // stable's release version adds leading zeros to month, 
            // but lazer only adds to day, so we add leading zeros to month to match stable's version format.
            string minor = version.Minor.ToString().PadLeft(4, '0');

            return $"{version.Major}{minor}{(version.Build > 0 ? $".{version.Build}" : string.Empty)}";
        }
    }

    private LegacyTextFlowContainer generalText = null!;

    [Resolved]
    private OsuGame? game { get; set; } = null;

    [Resolved]
    private BeatmapListingOverlay? beatmapListing { get; set; }

    [Resolved]
    private SettingsOverlay? settings { get; set; }

    [Resolved]
    private LoginOverlay? loginOverlay { get; set; }

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    private readonly IBindable<APIState> apiState = new Bindable<APIState>();

    private Sprite networkStatusSprite = null!;

    private Container fadeContainer = null!;

    [BackgroundDependencyLoader]
    private void load(OsuConfigManager config, TextureStore textures, RealmDetachedBeatmapStore beatmapStore)
    {
        RelativeSizeAxes = Axes.Both;

        parallaxEnabled = config.GetBindable<bool>(OsuSetting.MenuParallax);

        apiState.BindTo(api.State);

        InternalChildren = new Drawable[]
        {
            amplitudesProvider,
            fadeContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        RelativeSizeAxes = Axes.X,
                        Height = 54 * LegacyExperiencePlugin.StableRatio,
                        Alpha = 0.4f,
                        Colour = Colour4.Black,
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        RelativeSizeAxes = Axes.X,
                        Height = 54 * LegacyExperiencePlugin.StableRatio,
                        Alpha = 0.4f,
                        Colour = Colour4.Black,
                    },
                    userPanelContainer = new Container<LegacyUserPanel>
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        AutoSizeAxes = Axes.Both,
                    },
                    generalText = new LegacyTextFlowContainer(static t =>
                    {
                        t.Font = LegacyFont.Default.With(size: 14);
                        t.Shadow = true;
                    })
                    {
                        Position = new Vector2(210f, 0f) * LegacyExperiencePlugin.StableRatio,
                        ParagraphSpacing = 0,
                    },
                    new MusicControl
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                    },
                    osuDirectButton = new OsuDirectButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Alpha = 0,
                        Action = () => transitionScreen(() => beatmapListing?.Show()),
                    },
                    new FontText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.TopCentre,
                        Y = -52 * LegacyExperiencePlugin.StableRatio,
                        Shadow = true,
                        Font = LegacyFont.Default.With(size: 16),
                        Text = $"Welcome to osu!cuttingedge ({versionString})."
                    },
                    new CopyrightButton
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Margin = new MarginPadding
                        {
                            Bottom = 2 * LegacyExperiencePlugin.StableRatio,
                            Left = 2 * LegacyExperiencePlugin.StableRatio,
                        },
                        // this is ppy's official website, so it should be safe to open without warning.
                        // and it matches stable's behaviour where clicking the copyright opens the website without warning.
                        Action = () => game?.OpenUrlExternally("https://osu.ppy.sh/", LinkWarnMode.NeverWarn),
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding
                        {
                            Bottom = 30 / LegacyExperiencePlugin.StableRatio,
                            Right = 20 / LegacyExperiencePlugin.StableRatio,
                        },
                        Children = new Drawable[]
                        {
                            supporterIcon = new MenuIcon
                            {
                                Origin = Anchor.Centre,
                                Alpha = 0,
                                Scale = new Vector2(permission_icon_scale),
                                TooltipText = LegacyStrings.Menu_Supporter,
                                TextureName = "UI/menu-subscriber",
                            },
                            batIcon = new MenuIcon
                            {
                                Origin = Anchor.Centre,
                                X = -30,
                                Alpha = 0,
                                Scale = new Vector2(permission_icon_scale),
                                TooltipText = LegacyStrings.Menu_BAT,
                                TextureName = "UI/menu-bat",
                            },
                        }
                    },
                    new LegacyFpsDisplay
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                    }
                }
            },
            networkStatusSprite = new Sprite
            {
                Alpha = 0,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-80, -30) * LegacyExperiencePlugin.StableRatio,
                Texture = textures.GetAutoSized("UI/menu-connection"),
            },
            buttonSystem = new ButtonSystem
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                ParallaxEnabled = { BindTarget = parallaxEnabled },
            },
        };

        if (game is not null)
            screenStack = game.ScreenStack;

        localUser.BindTo(api.LocalUser);
        localUser.BindValueChanged(v => localUserChanged(v.NewValue), true);

        buttonSystem.OnEditClick = () =>
        {
            transitionScreen(() => PushScreen(new EditorLoader()));
            buttonSystem.FadeButtonsExcept("edit");
        };
        buttonSystem.OnOptionsClick = () => settings?.Show();
        buttonSystem.OnExitClick = () => screenStack?.Exit();

        buttonSystem.OnFreeplayClick = () =>
        {
            transitionScreen(() => PushScreen(new SoloSongSelect()));
            buttonSystem.FadeButtonsExcept("freeplay");
        };
        buttonSystem.OnMultiplayerClick = () =>
        {
            if (api.State.Value is not APIState.Online)
            {
                loginOverlay?.Show();
                return;
            }

            transitionScreen(() => PushScreen(new Multiplayer()));
            buttonSystem.FadeButtonsExcept("multiplayer");
        };

        apiState.BindValueChanged(apiStateChanged, true);

        beatmapSets = beatmapStore.GetBeatmapSets(null);

        beatmapSets.BindCollectionChanged((_, _) => beatmapCount = beatmapSets.Sum(static set => set.Beatmaps.Count), true);
    }

    private void apiStateChanged(ValueChangedEvent<APIState> @event)
    {
        switch (@event.NewValue)
        {
            case APIState.Failing:
                networkStatusSprite.FadeTo(0.6f, 500, Easing.None)
                                   .Then()
                                   .FadeTo(0.4f, 1600)
                                   .Loop(500);
                break;

            default:
                networkStatusSprite.ClearTransforms();
                networkStatusSprite.FadeOut(500);
                break;
        }
    }

    private OsuScreenStack? screenStack;

    private void PushScreen(IScreen screen)
    {
        screenStack?.Push(screen);
    }

    [Resolved]
    private TransitionManager transitionManager { get; set; } = null!;

    private void transitionScreen(Action action)
    {
        transitionManager.PlayTransition(action);
    }

    private const float permission_icon_scale = 0.25f;

    private void localUserChanged(APIUser user)
    {
        foreach (var panel in userPanelContainer.Children)
            panel.FadeOut(200).Expire();

        LegacyUserPanel newPanel;

        userPanelContainer.Add(newPanel = new LegacyUserPanel(user)
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
            ExtendedStyle = { Value = true },
            Action = () => loginOverlay?.Show(),
        });

        newPanel.FadeInFromZero(200);

        float delay = 250;

        // https://osu.ppy.sh/wiki/en/People/Beatmap_Appreciation_Team
        // it seems that BAT is a outdated term, I checked some previous BATs and they are either GMTs or QATs, so checking for both just in case.
        if (user.IsGMT || user.IsQAT)
        {
            batIcon.Delay(delay)
                   .FadeInFromZero(200, Easing.None)
                   .ScaleTo(1, 1000, Easing.OutElastic);

            delay += 100;
        }
        else
        {
            batIcon.FadeOut(100)
                   .Then()
                   .ScaleTo(permission_icon_scale);
        }

        if (user.IsSupporter)
        {
            supporterIcon.Delay(delay)
                         .FadeInFromZero(200, Easing.None)
                         .ScaleTo(1, 1000, Easing.OutElastic);

            osuDirectButton.FadeIn(200);
        }
        else
        {
            supporterIcon.FadeOut(100)
                         .Then()
                         .ScaleTo(permission_icon_scale);

            osuDirectButton.FadeOut(100);
        }
    }

    protected override void Update()
    {
        base.Update();

        updateGeneral();
    }

    private IBindableList<BeatmapSetInfo> beatmapSets = null!;
    private int beatmapCount = 0;

    [Resolved]
    private OsuGameBase gameBase { get; set; } = null!;

    private int lastSeconds;

    private void updateGeneral()
    {
        int runningSeconds = (int)gameBase.Time.Current / 1000;

        if (runningSeconds == lastSeconds)
            return;

        lastSeconds = runningSeconds;

        LocalisableString text;

        if (localUser.Value.Id != APIUser.SYSTEM_USER_ID)
        {
            int hours = runningSeconds / 3600000;
            int minutes = runningSeconds % 3600000 / 60000;
            int seconds = runningSeconds % 60000;

            LocalisableString runningTime;

            if (hours > 0)
                runningTime = $"{hours:00}:{minutes:00}:{seconds:00}";
            else if (minutes > 0)
                runningTime = $"{minutes}m {seconds}s";
            else
                runningTime = LegacyStrings.Menu_RunningSeconds(seconds);

            text = LegacyStrings.Menu_GeneralInformation(beatmapCount, runningTime, DateTime.Now.ToShortTimeString());
        }
        else
        {
            text = LegacyStrings.Menu_GeneralInformation_Offline(beatmapCount);
        }

        generalText.Text = text;
    }

    private partial class MenuIcon : Sprite, IHasLegacyTooltip
    {
        public required LocalisableString TooltipText { get; init; }
        public required string TextureName { get; init; }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            Texture = textures.GetAutoSized(TextureName);
        }
    }

    private partial class OsuDirectButton : CompositeDrawable
    {
        public Action? Action { get; set; }

        private Sprite spriteHover = null!;

        [Resolved]
        private AudioEngine audioEngine { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Sprite
                {
                    Texture = textures.GetAutoSized("UI/menu-osudirect"),
                },
                spriteHover = new Sprite
                {
                    Alpha = 0.01f,
                    BypassAutoSizeAxes = Axes.Both,
                    Texture = textures.GetAutoSized("UI/menu-osudirect-over"),
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            // FIXME: stable prefers menu-direct-hover, but i didn't find this asset.
            audioEngine.PlaySamplePositional(LegacySample.menuclick, null);
            spriteHover.FadeTo(1, 140);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            spriteHover.FadeTo(0.01f, 140);
        }

        protected override bool OnClick(ClickEvent e)
        {
            // FIXME: stable prefers menu-direct-click, but i didn't find this asset.
            audioEngine.PlaySamplePositional(LegacySample.menuhit, null);
            Action?.Invoke();
            return true;
        }
    }

    private partial class CopyrightButton : ClickableContainer
    {
        private Sprite sprite = null!;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                sprite = new Sprite
                {
                    Texture = textures.GetAutoSized("UI/menu-copyright"),
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            sprite.FadeColour(new Colour4(255, 179, 59, 255), 100, Easing.None)
                  .ScaleTo(1.15f, 180, Easing.Out)
                  .Then().ScaleTo(1.05f, 180, Easing.In)
                  .Then().ScaleTo(1.10f, 180, Easing.Out)
                  .Then().ScaleTo(1.07f, 180, Easing.In)
                  .Then().ScaleTo(1.085f, 180, Easing.Out);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            sprite.FadeColour(Colour4.White, 400, Easing.None)
                  .ScaleTo(1, 600, Easing.Out);

            base.OnHoverLost(e);
        }
    }
}
