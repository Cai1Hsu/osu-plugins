using System.Collections;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Chat.ChannelList;
using osu.Game.Plugins;

namespace osu.Plugin.Patches;

/// <summary>
/// This plugin automatically opens the multiplayer chat channels in the chat overlay, like how the old stable client did,
/// allowing players to easily keep track of the chat and receive notifications for new messages.
/// </summary>
public partial class MultiplayerChatPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        game.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var channelManager = game.Dependencies.Get<ChannelManager>();
            var chatOverlay = game.Dependencies.Get<ChatOverlay>();

            var trackingChannels = new Dictionary<long, Channel>();

            var joinedChannels = channelManager.JoinedChannels;

            joinedChannels.BindCollectionChanged((_, arg) =>
            {
                // of's BindableList is not thread safe, try our best to work around it by copying the items to a separate array before processing.
                var newChannels = copied(arg.NewItems).Cast<Channel>().Where(c => c.Type is ChannelType.Multiplayer);
                var oldChannels = copied(arg.OldItems).Cast<Channel>().Where(c => c.Type is ChannelType.Multiplayer);

                // there may be removal requests from server-side
                foreach (var c in oldChannels)
                    removeChannel(c);

                foreach (var c in newChannels)
                    registerNewChannel(c);
            }, true);

            void registerNewChannel(Channel c)
            {
                if (!trackingChannels.TryAdd(c.Id, c))
                    return;

                scheduler.AddOnce(c =>
                {
                    string name = c.Name;

                    try
                    {
                        var channelList = ChatOverlayAccessor.get_channelList(chatOverlay);

                        // cheat ChatOverlay into treating this as a public channel so that it doesn't get filtered out by the UI.
                        c.Type = display_section;

                        // give it a more descriptive name in the channel list, as the original name is just the room id which isn't very helpful.
                        c.Name = $"#Multiplayer ({name.TrimStart('#')})";
                        channelList?.AddChannel(c);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to add channel {c.Name} to the channel list.");
                    }
                    finally
                    {
                        // set it back to multiplayer so that roll command works.
                        c.Type = ChannelType.Multiplayer;
                        c.Name = name;
                    }

                    c.Joined.BindValueChanged(j =>
                    {
                        if (j.NewValue)
                            return;

                        removeChannel(c);
                    }, true);
                }, c);
            }

            void removeChannel(Channel c)
            {
                if (c.Joined.Value)
                    return;

                trackingChannels.Remove(c.Id);

                scheduler.AddOnce(c =>
                {
                    var channelList = ChatOverlayAccessor.get_channelList(chatOverlay);

                    // ensure proper removal from the channel list, as the type is used as an identifier for which section it belongs to.
                    c.Type = display_section;

                    channelList.RemoveChannel(c);
                }, c);
            }
        });
    }

    // WORKAROUND: 
    // There's no leave functionality for Team channels, preventing unintended leaving of channels.
    // Leaving should only be done through room leaving, which will dispose the channel entirely.
    // But this also make the channel at the team section of the channel list, which is more intuitive.
    private const ChannelType display_section = ChannelType.Team;

    private partial class ChatOverlayAccessor : ChatOverlay
    {
        [PrivateAccessor(PrivateAccessorKind.Field, Name = "channelList")]
        public extern static ref ChannelList get_channelList(ChatOverlay instance);
    }

    private Array copied(IList? list)
    {
        if (list is null)
            return Array.Empty<object>();

        lock (list.SyncRoot)
        {
            var copy = new object[list.Count];
            list.CopyTo(copy, 0);
            return copy;
        }
    }
}
