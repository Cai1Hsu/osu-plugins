using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Chat;
using osu.Game.Plugins;

namespace osu.Plugin.Referee;

public partial class RefereePlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var channelManager = game.Dependencies.Get<ChannelManager>();
            var endpoints = game.CreateEndpoints();

            game.InjectDependency<RefereeClient>(out var _, () => new OnlineRefereeClient(endpoints));
            game.InjectDependency(out var rch, () => new RefereeCommandHandler());

            var refereeChannel = new RefereeChannel(rch);
            rch.SetChannel(refereeChannel);

            var hook = new ChatOverlayHook(refereeChannel)
            {
                RefereeTextCommitHandler = rch.HandleCommand,
            };

            // without the hook rch still works, but we don't want to post unwanted message API calls
            game.Add(hook);

            channelManager.CurrentChannel.BindValueChanged(_ =>
             {
                 if (channelManager.CurrentChannel.Value != refereeChannel)
                     return;

                 refereeChannel.MessagesLoaded = true;
             }, true);

            channelManager.AvailableChannels.BindCollectionChanged((_, _) =>
            {
                var availableChannels = channelManager.AvailableChannels.ToArray();

                if (availableChannels.OfType<RefereeChannel>().Any())
                    return;

                refereeChannel.MessagesLoaded = true;
            }, true);

            channelManager.JoinedChannels.BindCollectionChanged((_, _) =>
            {
                var joinedChannels = channelManager.JoinedChannels.ToArray();

                if (!joinedChannels.OfType<RefereeChannel>().Any())
                {
                    refereeChannel.MessagesLoaded = true;
                    channelManager.JoinChannel(refereeChannel);
                }
            }, true);
        });
    }

    private partial class ChatOverlayHook : Component
    {
        private readonly RefereeChannel refereeChannel;

        public ChatOverlayHook(RefereeChannel refereeChannel)
        {
            this.refereeChannel = refereeChannel;
        }

        [Resolved]
        private ChatOverlay chatOverlay { get; set; } = null!;

        [Resolved]
        private ChannelManager channelManager { get; set; } = null!;

        private readonly Bindable<Channel?> currentChannel = new Bindable<Channel?>();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            chatOverlay.InvokeWhenReady(d =>
            {
                currentChannel.BindTo(channelManager.CurrentChannel);
                currentChannel.BindValueChanged(updateCommitMethod, true);

                var defaultHandler_MethodInfo = typeof(ChatOverlay)
                    .GetMethod("handleChatMessage", BindingFlags.Instance | BindingFlags.NonPublic);

                defaultTextCommitHandler = (Action<string>)Delegate.CreateDelegate(
                    typeof(Action<string>),
                    chatOverlay,
                    defaultHandler_MethodInfo);
            });
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "textBar")]
        private static extern ref ChatTextBar get_TextBar(ChatOverlay chatOverlay);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = nameof(ChatTextBar.OnChatMessageCommitted))]
        private static extern ref Action<string> get_OnChatMessageCommitted(ChatTextBar textBar);

        private Action<string>? defaultTextCommitHandler = _ => { };

        public Action<string> RefereeTextCommitHandler { get; set; } = null!;

        private void updateCommitMethod(ValueChangedEvent<Channel?> @event)
        {
            var newChannel = @event.NewValue;

            var textBar = get_TextBar(chatOverlay);

            ref var onChatMessageCommitted = ref get_OnChatMessageCommitted(textBar);

            if (newChannel is RefereeChannel referee)
            {
                if (referee == refereeChannel)
                    onChatMessageCommitted = RefereeTextCommitHandler;
                else
                    onChatMessageCommitted = static _ => { };
            }
            else
            {
                onChatMessageCommitted = defaultTextCommitHandler;
            }
        }
    }
}
