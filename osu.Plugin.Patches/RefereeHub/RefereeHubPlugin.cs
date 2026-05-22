using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Online.Chat;
using osu.Game.Plugins;
using osu.Plugin.Patches.Referee;

namespace osu.Plugin.Patches.RefereeHub;

public partial class RefereeHubPlugin : OsuPlugin
{
    // store in config for persistence

    [SettingSource("Client Id")]
    public Bindable<string> ClientId { get; } = new Bindable<string>();

    [SettingSource("Client Secret")]
    public Bindable<string> ClientSecret { get; } = new Bindable<string>();

    [SettingSource("Token")]
    public Bindable<string> Token { get; } = new Bindable<string>();

    private CustomAPIAccess api = null!;

    private const string api_scope = "multiplayer.write_manage";

    private readonly IBindable<bool> HubConnected = new Bindable<bool>();

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        var endpoints = gameBase.CreateEndpoints();

        api = new CustomAPIAccess(endpoints, api_scope)
        {
            // bind token first so that recreateOauth can access the saved token value if it exists.
            TokenString = { BindTarget = Token },
            ClientId = { BindTarget = ClientId },
            ClientSecret = { BindTarget = ClientSecret },
        };

        string hubEndpoints = replaceHubTail(endpoints.SpectatorUrl, "Spectator", "Referee");

        gameBase.InvokeWhenReady(d =>
        {
            var game = (OsuGameBase)d;

            game.CacheDependency(out var client, () => new OnlineRefereeClient(hubEndpoints, api), true);
            game.CacheDependency(out var console, () => new RefereeConsole(client), true);

            HubConnected.BindTo(client.IsConnected);

            var channelManager = game.Dependencies.Get<ChannelManager>();
            var currentChannel = channelManager?.CurrentChannel;

            currentChannel.BindValueChanged(v =>
            {
                if (v.OldValue is { } prev)
                    prev.NewMessagesArrived -= onMessageArrives;

                if (v.NewValue is { } newChannel)
                    newChannel.NewMessagesArrived += onMessageArrives;
            });

            void onMessageArrives(IEnumerable<Message> messages)
            {
                if (currentChannel.Value is not { } channel)
                    return;

                var inputMessage = messages.OfType<LocalEchoMessage>();

                foreach (var message in inputMessage)
                {
                    bool localEchoed = false;

                    void fireMessage(Message? message, object? tag)
                    {
                        if (channel.Id < 0 && !localEchoed && tag is LocalEchoMessage localEcho)
                        {
                            localEchoed = true;

                            channel.ReplaceMessage(localEcho, new Message(localEcho.Id)
                            {
                                ChannelId = localEcho.ChannelId,
                                Content = localEcho.Content,
                                Sender = localEcho.Sender,
                                Timestamp = localEcho.Timestamp,
                                IsAction = localEcho.IsAction,
                                Links = localEcho.Links,
                                Uuid = localEcho.Uuid,
                                DisplayContent = localEcho.DisplayContent
                            });
                        }

                        if (message != null)
                        {
                            channel.NewMessagesArrived -= onMessageArrives;
                            channel.AddNewMessages(message);
                            channel.NewMessagesArrived += onMessageArrives;
                        }
                    }

                    console.CommandFired += fireMessage;
                    console.HandleCommand(message.Content, message);
                    console.CommandFired -= fireMessage;
                }
            }
        });
    }

    static string replaceHubTail(string endpoint, string from, string to)
    {
        string needle = "/" + from;

        if (endpoint.EndsWith(needle, StringComparison.OrdinalIgnoreCase))
            return endpoint.Substring(0, endpoint.Length - needle.Length) + "/" + to;

        return endpoint;
    }


    public override IEnumerable<Drawable>? CreateSettingsControls()
    {
        return new Drawable[]
        {
            new LoginSection(api)
            {
                ClientId = { BindTarget = ClientId },
                ClientSecret = { BindTarget = ClientSecret },
                HubConnected = { BindTarget = HubConnected }
            }
        };
    }
}
