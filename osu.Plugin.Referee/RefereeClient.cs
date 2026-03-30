using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online;
using osu.Server.Spectator.Hubs.Referee;
using osu.Server.Spectator.Hubs.Referee.Models.Events;
using osu.Server.Spectator.Hubs.Referee.Models.Requests;
using osu.Server.Spectator.Hubs.Referee.Models.Responses;

namespace osu.Plugin.Referee;

public abstract partial class RefereeClient : Component, IRefereeHubClient, IStatefulUserHubClient, IRefereeHubServer
{
    public abstract IBindable<bool> IsConnected { get; }

    public event Action<string>? PongReceived;
    public event Action<UserJoinedEvent>? UserJoinedReceived;
    public event Action<UserLeftEvent>? UserLeftReceived;
    public event Action<UserKickedEvent>? UserKickedReceived;
    public event Action<UserBannedEvent>? UserBannedReceived;
    public event Action<RefereeAddedEvent>? RefereeAddedReceived;
    public event Action<RefereeRemovedEvent>? RefereeRemovedReceived;
    public event Action<RefereeInvitedEvent>? RefereeInvitedReceived;
    public event Action<RoomSettingsChangedEvent>? RoomSettingsChangedReceived;
    public event Action<MatchStateChangedEvent>? MatchStateChangedReceived;
    public event Action<PlaylistItemAddedEvent>? PlaylistItemAddedReceived;
    public event Action<PlaylistItemChangedEvent>? PlaylistItemChangedReceived;
    public event Action<PlaylistItemRemovedEvent>? PlaylistItemRemovedReceived;
    public event Action<RollCompletedEvent>? RollCompletedReceived;
    public event Action<UserStatusChangedEvent>? UserStatusChangedReceived;
    public event Action<UserModsChangedEvent>? UserModsChangedReceived;
    public event Action<UserStyleChangedEvent>? UserStyleChangedReceived;
    public event Action<UserTeamChangedEvent>? UserTeamChangedReceived;
    public event Action<CountdownStartedEvent>? CountdownStartedReceived;
    public event Action<CountdownStoppedEvent>? CountdownStoppedReceived;
    public event Action<MatchStartedEvent>? MatchStartedReceived;
    public event Action<MatchAbortedEvent>? MatchAbortedReceived;
    public event Action<MatchCompletedEvent>? MatchCompletedReceived;
    public event Action? Disconnecting;

    #region IRefereeHubClient

    Task IRefereeHubClient.CountdownStarted(CountdownStartedEvent info)
    {
        Schedule(() => CountdownStartedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.CountdownStopped(CountdownStoppedEvent info)
    {
        Schedule(() => CountdownStoppedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.MatchAborted(MatchAbortedEvent info)
    {
        Schedule(() => MatchAbortedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.MatchCompleted(MatchCompletedEvent info)
    {
        Schedule(() => MatchCompletedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.MatchStarted(MatchStartedEvent info)
    {
        Schedule(() => MatchStartedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.MatchStateChanged(MatchStateChangedEvent info)
    {
        Schedule(() => MatchStateChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.PlaylistItemAdded(PlaylistItemAddedEvent info)
    {
        Schedule(() => PlaylistItemAddedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.PlaylistItemChanged(PlaylistItemChangedEvent info)
    {
        Schedule(() => PlaylistItemChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.PlaylistItemRemoved(PlaylistItemRemovedEvent info)
    {
        Schedule(() => PlaylistItemRemovedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.Pong(string message)
    {
        Schedule(() => PongReceived?.Invoke(message));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.RefereeAdded(RefereeAddedEvent info)
    {
        Schedule(() => RefereeAddedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.RefereeInvited(RefereeInvitedEvent info)
    {
        Schedule(() => RefereeInvitedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.RefereeRemoved(RefereeRemovedEvent info)
    {
        Schedule(() => RefereeRemovedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.RollCompleted(RollCompletedEvent info)
    {
        Schedule(() => RollCompletedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.RoomSettingsChanged(RoomSettingsChangedEvent info)
    {
        Schedule(() => RoomSettingsChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserBanned(UserBannedEvent info)
    {
        Schedule(() => UserBannedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserJoined(UserJoinedEvent info)
    {
        Schedule(() => UserJoinedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserKicked(UserKickedEvent info)
    {
        Schedule(() => UserKickedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserLeft(UserLeftEvent info)
    {
        Schedule(() => UserLeftReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserModsChanged(UserModsChangedEvent info)
    {
        Schedule(() => UserModsChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserStatusChanged(UserStatusChangedEvent info)
    {
        Schedule(() => UserStatusChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserStyleChanged(UserStyleChangedEvent info)
    {
        Schedule(() => UserStyleChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    Task IRefereeHubClient.UserTeamChanged(UserTeamChangedEvent info)
    {
        Schedule(() => UserTeamChangedReceived?.Invoke(info));
        return Task.CompletedTask;
    }

    #endregion

    #region IStatefulUserHubClient

    Task IStatefulUserHubClient.DisconnectRequested()
    {
        Schedule(() => _ = DisconnectInternal());
        return Task.CompletedTask;
    }

    protected virtual Task DisconnectInternal()
    {
        Disconnecting?.Invoke();
        return Task.CompletedTask;
    }

    #endregion

    #region RPC methods

    public abstract Task Ping(string message);
    public abstract Task<RoomJoinedResponse> MakeRoom(MakeRoomRequest request);
    public abstract Task<RoomJoinedResponse> JoinRoom(long roomId);
    public abstract Task LeaveRoom(long roomId);
    public abstract Task CloseRoom(long roomId);
    public abstract Task InvitePlayer(long roomId, int userId);
    public abstract Task KickPlayer(long roomId, int userId);
    public abstract Task BanUser(long roomId, int bannedUserId);
    public abstract Task AddReferee(long roomId, int targetUserId);
    public abstract Task RemoveReferee(long roomId, int targetUserId);
    public abstract Task ChangeRoomSettings(long roomId, ChangeRoomSettingsRequest request);
    public abstract Task EditCurrentPlaylistItem(long roomId, EditCurrentPlaylistItemRequest request);
    public abstract Task AddPlaylistItem(long roomId, AddPlaylistItemRequest request);
    public abstract Task EditPlaylistItem(long roomId, EditPlaylistItemRequest request);
    public abstract Task RemovePlaylistItem(long roomId, RemovePlaylistItemRequest request);
    public abstract Task Roll(long roomId, RollRequest request);
    public abstract Task MoveUser(long roomId, MoveUserRequest request);
    public abstract Task SetLockState(long roomId, SetLockStateRequest request);
    public abstract Task StartMatch(long roomId, StartGameplayRequest request);
    public abstract Task StopMatchCountdown(long roomId);
    public abstract Task AbortMatch(long roomId);

    #endregion
}
