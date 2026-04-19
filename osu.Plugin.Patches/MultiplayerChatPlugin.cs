using System.Collections;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Online.Rooms;
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

            var api = game.Dependencies.Get<IAPIProvider>();

            var channelManager = game.Dependencies.Get<ChannelManager>();
            var chatOverlay = game.Dependencies.Get<ChatOverlay>();

            var trackingChannels = new Dictionary<long, Bindable<bool>>();

            var currentChannel = channelManager.CurrentChannel;
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
                {
                    if (!long.TryParse(trimChannelName(c.Name), out long roomId))
                        continue;

                    // osu-spectator-server removes players from the channel via interop with osu-web,
                    // and this operation may fail and cause the player to remain in the channel without actually being able to receive messages.
                    // in this case, those *ghost* channels are still joined,
                    // even though we can still receive and send messages to them, 
                    // we should not display them in the channel list as they are not functional and will just cause confusion.

                    var req = new GetRoomRequest(roomId);

                    req.Failure += e =>
                    {
                        Logger.Error(e, $"Failed to get room info for channel {c.Name}, skipping channel registration.");
                    };

                    req.Success += r =>
                    {
                        if (!r.HasEnded)
                            scheduler.AddOnce(c => registerNewChannel(c, r), c);
                        else
                            Logger.Log($"Room {r.RoomID} has already ended, skipping channel registration.", LoggingTarget.Runtime, LogLevel.Verbose);
                    };

                    api.Queue(req);
                }
            }, true);

            void registerNewChannel(Channel c, Room r)
            {
                if (trackingChannels.ContainsKey(c.Id))
                    return;

                var joined = c.Joined.GetBoundCopy();
                trackingChannels.Add(c.Id, joined);

                scheduler.AddOnce(c =>
                {
                    string name = c.Name;
                    var originalType = c.Type;

                    try
                    {
                        var channelList = ChatOverlayAccessor.get_channelList(chatOverlay);

                        // cheat ChatOverlay into treating this as a public channel so that it doesn't get filtered out by the UI.
                        c.Type = display_section;

                        // give it a more descriptive name in the channel list, as the original name is just the room id which isn't very helpful.
                        c.Name = $"#Multiplayer ({r.RoomID}, {r.Name})"; // the topic is the room name
                        channelList?.AddChannel(c);

                        // set the current channel to the newly joined multiplayer channel for smoother experience, 
                        // as players are likely to want to see the multiplayer chat immediately after joining a room.
                        currentChannel.Value = c;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to add channel {c.Name} to the channel list.");
                    }
                    finally
                    {
                        // set it back to multiplayer so that roll command works.
                        c.Type = originalType;
                        c.Name = name;
                    }

                    joined.BindValueChanged(j =>
                    {
                        if (j.NewValue)
                            return;

                        removeChannel(c);
                    }, true);
                }, c);
            }

            void removeChannel(Channel c)
            {
                if (trackingChannels.Remove(c.Id, out var joined))
                    joined.UnbindAll();

                var originalType = c.Type;

                scheduler.AddOnce(c =>
                {
                    try
                    {
                        var channelList = ChatOverlayAccessor.get_channelList(chatOverlay);

                        // ensure proper removal from the channel list, as the type is used as an identifier for which section it belongs to.
                        c.Type = display_section;

                        channelList.RemoveChannel(c);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to remove channel {c.Name} from the channel list.");
                    }
                    finally
                    {
                        // restore the original type.
                        c.Type = originalType;
                    }
                }, c);
            }
        });
    }

    private string trimChannelName(string name)
    {
        const string prefix = "#lazermp_";

        var index = name.IndexOf(prefix, StringComparison.Ordinal);

        if (index < 0)
            return name;

        // extract room id from channel name, which is in the format of #lazermp_{roomId}
        return name[(index + prefix.Length)..];
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
