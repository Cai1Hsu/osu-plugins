using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Chat;
using osu.Game.Overlays.Settings;
using osu.Game.Online.API;
using osuTK;
using osu.Game.Overlays;

namespace osu.Plugin.Patches.RefereeHub;

public partial class LoginSection : CompositeDrawable
{
    private readonly CustomAPIAccess api;
    public readonly Bindable<string> ClientId = new Bindable<string>();
    public readonly Bindable<string> ClientSecret = new Bindable<string>();
    public readonly IBindable<bool> HubConnected = new Bindable<bool>(true);

    public LoginSection(CustomAPIAccess api)
    {
        this.api = api;

        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
    }

    private FillFlowContainer loginFields = null!;
    private Container loggingContainer = null!;
    private FillFlowContainer logoutFields = null!;

    private FormTextBox grantCodeForm = null!;
    private FormTextBox clientIdForm = null!;
    private FormTextBox clientSecretForm = null!;
    private SettingsButtonV2 authButton = null!;
    private SettingsButtonV2 logoutButton = null!;
    private TextFlowContainer errorText = null!;
    private LoadingSpinner spinner = null!;
    private OsuSpriteText loggingInText = null!;
    private OsuSpriteText successText = null!;
    private Drawable hubConnection = null!;

    [Resolved]
    private OsuGame? game { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChildren = new Drawable[]
        {
            loginFields = new FillFlowContainer
            {
                Name = "Login fields",
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                LayoutDuration = 200,
                LayoutEasing = Easing.OutQuint,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new SettingsItemV2(clientIdForm = new FormTextBox
                    {
                        Caption = "Client Id"
                    }),
                    new SettingsItemV2(clientSecretForm = new FormTextBox
                    {
                        Caption = "Client Secret"
                    }),
                    authButton = new SettingsButtonV2
                    {
                        Text = "Auth in browser",
                    },
                    new SettingsItemV2(grantCodeForm = new FormTextBox
                    {
                        Caption = "Grant Code (Enter to submit)",
                        HintText = "The code you get after logging in through the browser, or simply paste the url you are redirected to after login.",
                    }),
                    errorText = new OsuTextFlowContainer(cp => cp.Colour = Colour4.Red)
                    {
                        AutoSizeAxes = Axes.Both,
                        Padding = SettingsPanel.CONTENT_PADDING,
                    }
                }
            },
            loggingContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            spinner = new LoadingSpinner
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            },
                            loggingInText = new OsuSpriteText
                            {
                                Text = "Authenticating, please wait...",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            },
                        }
                    },
                    successText = new OsuSpriteText
                    {
                        Text = "Authentication successful!",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                    },
                }
            },
            logoutFields = new FillFlowContainer
            {
                Name = "Logout fields",
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                LayoutDuration = 200,
                LayoutEasing = Easing.OutQuint,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    hubConnection = new FillFlowContainer()
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            new LoadingSpinner
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            }.With(d => d.Show()),
                            new OsuSpriteText
                            {
                                Text = "Connecting to referee hub...",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            },
                        }
                    },
                    new OsuTextFlowContainer()
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Text = "You are currently authenticated. If you want to switch accounts, please logout first."
                    },
                    logoutButton = new SettingsButtonV2
                    {
                        Text = "Logout",
                    },
                }
            }
        };

        clientIdForm.Current.BindTo(ClientId);
        clientSecretForm.Current.BindTo(ClientSecret);

        authButton.Action = () =>
        {
            game?.OpenUrlExternally(api.CodeGrantUrl, LinkWarnMode.Default);
        };

        logoutButton.Action = () => api.Logout();

        grantCodeForm.ValueChanged += onGrantCodeChanged;
        grantCodeForm.OnCommit += onGrantCodeCommit;

        api.State.BindValueChanged(state => Scheduler.AddOnce(updateState, state.NewValue), true);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        HubConnected.BindValueChanged(v => Scheduler.AddOnce(connected =>
        {
            hubConnection.FadeTo(connected ? 0 : 1, 200, Easing.OutQuint);
        }, v.NewValue), true);
    }

    private void updateState(APIState state)
    {
        switch (state)
        {
            case APIState.Connecting:
                errorText.Text = string.Empty;

                loginFields.FadeOut(200, Easing.OutQuint);
                loggingContainer.FadeIn(200, Easing.OutQuint);

                spinner.FadeIn(200, Easing.OutQuint);
                loggingInText.FadeIn(200, Easing.OutQuint);

                successText.FadeOut();
                break;

            case APIState.Online:
                loginFields.FadeOut(200, Easing.OutQuint);

                spinner.FadeOut(200, Easing.OutQuint);
                loggingInText.FadeOut(200, Easing.OutQuint);
                successText.FadeIn(200, Easing.OutQuint);

                using (BeginDelayedSequence(1500))
                {
                    loggingContainer.FadeOut(200, Easing.OutQuint);
                    logoutFields.FadeIn(200, Easing.OutQuint);
                }
                break;

            case APIState.Offline:
                loggingContainer.FadeOut(200, Easing.OutQuint);
                logoutFields.FadeOut(200, Easing.OutQuint);
                loginFields.FadeIn(200, Easing.OutQuint);
                break;
        }
    }

    private void onGrantCodeChanged()
    {
        var content = grantCodeForm.Current.Value;

        var uri = new Uri(content, UriKind.Absolute);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        if (query.Get("code") is { } code && string.IsNullOrEmpty(code))
            grantCodeForm.Current.Value = code;
    }

    private async void onGrantCodeCommit(TextBox sender, bool newText)
    {
        var content = sender.Text;

        if (string.IsNullOrEmpty(content))
            return;

        // in case user pasted the whole url, extract the code from the url.
        var uri = new Uri(content, UriKind.Absolute);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var grantCode = query.Get("code") ?? content;

        try
        {
            await api.AuthenticateWithCodeGrant(grantCode);
        }
        catch (Exception e)
        {
            Scheduler.AddOnce(() =>
            {
                errorText.Text = e.Message;
            });
        }
    }
}
