using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Server.Spectator.Hubs.Referee.Models;
using osu.Server.Spectator.Hubs.Referee.Models.Requests;
using RollRequest = osu.Server.Spectator.Hubs.Referee.Models.Requests.RollRequest;
using SetLockStateRequest = osu.Server.Spectator.Hubs.Referee.Models.Requests.SetLockStateRequest;

namespace osu.Plugin.Referee;

public partial class RefereeCommandHandler : Component
{
    [Resolved]
    private RefereeClient refereeClient { get; set; } = null!;

    [Resolved]
    private MultiplayerClient multiplayerClient { get; set; } = null!;

    private RefereeChannel? refereeChannel = null;

    public void SetChannel(RefereeChannel channel)
    {
        refereeChannel = channel;
    }

    private readonly IBindable<bool> clientConnected = new BindableBool();

    protected override void LoadComplete()
    {
        base.LoadComplete();

        refereeClient.PongReceived += s => Schedule(() => reply(s));
        refereeClient.UserJoinedReceived += e => Schedule(() => reply($"User joined: {e.UserId} in room {e.RoomId}"));
        refereeClient.UserLeftReceived += e => Schedule(() => reply($"User left: {e.UserId} from room {e.RoomId}"));
        refereeClient.UserKickedReceived += e => Schedule(() => reply($"User {e.KickingUserId} kicked: {e.KickedUserId} from room {e.RoomId}"));
        refereeClient.UserBannedReceived += e => Schedule(() => reply($"User {e.BanningUserId} banned: {e.BannedUserId} from room {e.RoomId}"));
        refereeClient.RefereeAddedReceived += e => Schedule(() => reply($"Referee added: {e.UserId} to room {e.RoomId}"));
        refereeClient.RefereeRemovedReceived += e => Schedule(() => reply($"Referee removed: {e.UserId} from room {e.RoomId}"));
        refereeClient.RefereeInvitedReceived += e => Schedule(() => reply($"Room {e.RoomId} invited you to be a referee."));
        refereeClient.RoomSettingsChangedReceived += e => Schedule(() => reply($"Room {e.RoomId} settings changed."));
        // TODO: add more event handlers as needed

        refereeClient.RollCompletedReceived += e => Schedule(() => reply($"User {e.UserId} rolled rolled {e.Result} (max {e.Max}) in room {e.RoomId}."));

        clientConnected.BindTo(refereeClient.IsConnected);

        clientConnected.BindValueChanged(v =>
        {
            if (v.NewValue)
                reply("Connected to referee hub.");
            else
                reply("Disconnected from referee hub.");
        });

        if (refereeClient.IsConnected.Value)
            reply("Connected to referee hub.");
    }

    private void reply(string content)
    {
        var message = new Message()
        {
            Content = content,
            DisplayContent = content,
            Sender = APIUser.SYSTEM_USER,
            Timestamp = DateTimeOffset.Now,
        };

        refereeChannel?.AddNewMessages(new[] { message });
    }

    class NotImRoomException : Exception
    {
        public NotImRoomException() : base("You must be in a multiplayer room to use this command.")
        {
        }
    }

    private MultiplayerRoom ensureInRoom()
    {
        if (multiplayerClient.Room is null)
            throw new NotImRoomException();

        return multiplayerClient.Room;
    }

    class NotConnectedException : Exception
    {
        public NotConnectedException() : base("Not connected to referee hub.")
        {
        }
    }

    private void ensureConnected()
    {
        if (!refereeClient.IsConnected.Value)
            throw new NotConnectedException();
    }

    public void HandleCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        try
        {
            handleCommand(cmd, args);
        }
        catch (NotImRoomException ex)
        {
            reply(ex.Message);
        }
        catch (Exception ex)
        {
            reply($"Error handling command: {ex.Message}");
        }
    }

    private int localUserId => api.LocalUser.Value.OnlineID;

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    private int parseUserId(string input, bool allowSelf = true)
    {
        if (input.Equals("self", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!allowSelf)
                throw new ArgumentException("The 'self' identifier is not allowed in this context.");

            return localUserId;
        }

        if (!int.TryParse(input, out int userId))
            throw new ArgumentException("Invalid user ID.");

        return userId;
    }

    private void handleCommand(string cmd, string[] args)
    {

        switch (cmd)
        {
            case "ping":
                {
                    ensureConnected();

                    string content = args.ElementAtOrDefault(0) ?? string.Empty;

                    refereeClient.Ping(content).FireAndForget(
                        onError: ex => reply($"Failed to ping: {ex.Message}"));
                }
                break;

            case "room":
                {
                    if (multiplayerClient.Room is null)
                        reply("Not currently in a multiplayer room.");
                    else
                        reply($"Currently in room {multiplayerClient.Room.RoomID}.");
                }
                break;

            case "make":
                {
                    ensureConnected();

                    int ruleset = int.Parse(args.ElementAtOrDefault(0) ?? "0");
                    int beatmap = int.Parse(args.ElementAtOrDefault(1) ?? "0");
                    string roomName = args.ElementAtOrDefault(2) ?? "Referee Room";

                    var makeReq = new MakeRoomRequest()
                    {
                        RulesetId = ruleset,
                        BeatmapId = beatmap,
                        RoomName = roomName
                    };

                    // TODO: use the return value
                    refereeClient.MakeRoom(makeReq).FireAndForget(
                        onSuccess: t => reply($"Made room {t.Name} (id: {t.RoomId}) with password: {t.Password}."),
                        onError: ex => reply($"Failed to make room: {ex.Message}"));
                }
                break;

            case "join":
                {
                    ensureConnected();

                    if (args.Length != 1 || !long.TryParse(args[0], out long roomId))
                    {
                        reply("Usage: join <roomId>");
                        break;
                    }

                    refereeClient.JoinRoom(roomId).FireAndForget(
                        onSuccess: () => reply($"Joined room {roomId}."),
                        onError: ex => reply($"Failed to join room: {ex.Message}"));
                }
                break;

            case "leave":
                {
                    ensureConnected();

                    var roomToLeave = ensureInRoom();
                    refereeClient.LeaveRoom(roomToLeave.RoomID).FireAndForget(
                        onSuccess: () => reply($"Left room {roomToLeave.RoomID}."),
                        onError: ex => reply($"Failed to leave room: {ex.Message}"));
                }
                break;

            case "close":
                {
                    ensureConnected();

                    var roomToClose = ensureInRoom();
                    refereeClient.CloseRoom(roomToClose.RoomID).FireAndForget(
                        onSuccess: () => reply($"Closed room {roomToClose.RoomID}."),
                        onError: ex => reply($"Failed to close room: {ex.Message}"));
                }
                break;

            case "invite":
                {
                    ensureConnected();

                    int roomId = int.Parse(args[0]);
                    int userId = parseUserId(args[1]);

                    refereeClient.InvitePlayer(roomId, userId).FireAndForget(
                        onSuccess: () => reply($"Invited user {userId} to room {roomId}."),
                        onError: ex => reply($"Failed to invite player: {ex.Message}"));
                }
                break;

            case "roll":
                ensureConnected();

                var room = ensureInRoom();

                if (args.Length != 1 || !uint.TryParse(args[0], out uint sides))
                {
                    reply("Usage: roll <sides>");
                    break;
                }

                var req = new RollRequest()
                {
                    Max = sides
                };

                refereeClient.Roll(room.RoomID, req).FireAndForget(
                    onSuccess: () => reply($"Rolled a {sides}-sided die."),
                    onError: ex => reply($"Failed to roll: {ex.Message}"));
                break;

            case "addref":
                {
                    ensureConnected();

                    int roomId = int.Parse(args[0]);
                    int userId = parseUserId(args[1]);

                    refereeClient.AddReferee(roomId, userId).FireAndForget(
                        onSuccess: () => reply($"Added user {userId} as a referee to room {roomId}."),
                        onError: ex => reply($"Failed to add referee: {ex.Message}"));
                }
                break;

            case "lock":
                {
                    ensureConnected();

                    int roomId = int.Parse(args[0]);
                    bool locked = bool.Parse(args[1]);

                    var lockReq = new SetLockStateRequest()
                    {
                        Locked = locked
                    };

                    refereeClient.SetLockState(roomId, lockReq).FireAndForget(
                        onSuccess: () => reply($"{(locked ? "Locked" : "Unlocked")} room {roomId}."),
                        onError: ex => reply($"Failed to change lock state: {ex.Message}"));
                }
                break;

            case "move":
                {
                    ensureConnected();

                    int roomId = int.Parse(args[0]);
                    int userId = parseUserId(args[1]);
                    var team = (MatchTeam)int.Parse(args[2]);

                    var moveReq = new MoveUserRequest()
                    {
                        UserId = userId,
                        Team = team
                    };

                    refereeClient.MoveUser(roomId, moveReq).FireAndForget(
                        onSuccess: () => reply($"Moved user {userId} to team {team} in room {roomId}."),
                        onError: ex => reply($"Failed to move user: {ex.Message}"));
                }
                break;


            case "removeref":
                {
                    ensureConnected();

                    int roomId = int.Parse(args[0]);
                    int userId = parseUserId(args[1]);

                    refereeClient.RemoveReferee(roomId, userId).FireAndForget(
                        onSuccess: () => reply($"Removed user {userId} as a referee from room {roomId}."),
                        onError: ex => reply($"Failed to remove referee: {ex.Message}"));
                }
                break;

            case "auth":
                {
                    var token = args.ElementAtOrDefault(0);

                    if (token is null)
                    {
                        reply("Usage: auth <access_token>");
                    }
                    else
                    {
                        if (refereeClient is OnlineRefereeClient onlineClient)
                        {
                            onlineClient.AccessToken.Value = token;

                            reply("Access token set.");
                        }
                        else
                        {
                            reply("Current client does not support authentication.");
                        }
                    }
                }

                // prevent leaking sensitive tokens
                foreach (var msg in refereeChannel?.Messages.ToArray() ?? Array.Empty<Message>())
                {
                    if (msg.Content.StartsWith("auth ", StringComparison.InvariantCultureIgnoreCase))
                        refereeChannel?.RemoveMessage(msg);
                }
                break;

            case "clear":
                {
                    if (refereeChannel is null)
                    {
                        reply("Referee channel not initialized.");
                        break;
                    }

                    foreach (var msg in refereeChannel.Messages.ToArray())
                        refereeChannel.RemoveMessage(msg);
                }
                break;

            default:
                reply($"Unknown command: {cmd}");
                break;
        }
    }
}
