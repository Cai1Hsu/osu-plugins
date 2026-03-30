using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Server.Spectator.Hubs.Referee;
using osu.Server.Spectator.Hubs.Referee.Models.Events;
using osu.Server.Spectator.Hubs.Referee.Models.Requests;
using osu.Server.Spectator.Hubs.Referee.Models.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using osu.Game.Online.Multiplayer;

namespace osu.Plugin.Referee;

public partial class OnlineRefereeClient : RefereeClient
{
    private readonly string endpoint = null!;

    public string Endpoint => endpoint;

    private IHubClientConnector? connector;

    public override IBindable<bool> IsConnected { get; } = new BindableBool();

    public Bindable<string?> AccessToken = new Bindable<string?>();

    private HubConnection? connection => connector?.CurrentConnection;

    public OnlineRefereeClient(string endpoint) : this()
    {
        this.endpoint = endpoint;
    }

    public OnlineRefereeClient(EndpointConfiguration endpoints) : this()
    {
        static string getRefereeEndpoint(EndpointConfiguration endpoints)
        {
            static string replaceHubTail(string endpoint, string from, string to)
            {
                string needle = "/" + from;
                if (endpoint.EndsWith(needle, StringComparison.OrdinalIgnoreCase))
                    return endpoint.Substring(0, endpoint.Length - needle.Length) + "/" + to;

                return endpoint;
            }

            if (!string.IsNullOrEmpty(endpoints.SpectatorUrl))
                return replaceHubTail(endpoints.SpectatorUrl, "spectator", "referee");

            if (!string.IsNullOrEmpty(endpoints.MultiplayerUrl))
                return replaceHubTail(endpoints.MultiplayerUrl, "multiplayer", "referee");

            return endpoints.MetadataUrl;
        }

        endpoint = getRefereeEndpoint(endpoints);
    }

    private OnlineRefereeClient()
    {
        AccessToken.Value = Environment.GetEnvironmentVariable("OSU_REFEREE_HUB_ACCESS_TOKEN");
    }

    [BackgroundDependencyLoader]
    private void load(IAPIProvider api)
    {
        connector = new CustomAuthencationHubClientConnector(nameof(OnlineRefereeClient), endpoint, api)
        {
            AccessToken = { BindTarget = AccessToken }
        };

        if (connector == null)
            return;

        connector.ConfigureConnection = connection =>
        {

#pragma warning disable CS0618 // Type or member is obsolete
            connection.On<string>(nameof(IRefereeHubClient.Pong), ((IRefereeHubClient)this).Pong);
#pragma warning restore CS0618

            connection.On<UserJoinedEvent>(nameof(IRefereeHubClient.UserJoined), ((IRefereeHubClient)this).UserJoined);
            connection.On<UserLeftEvent>(nameof(IRefereeHubClient.UserLeft), ((IRefereeHubClient)this).UserLeft);
            connection.On<UserKickedEvent>(nameof(IRefereeHubClient.UserKicked), ((IRefereeHubClient)this).UserKicked);
            connection.On<UserBannedEvent>(nameof(IRefereeHubClient.UserBanned), ((IRefereeHubClient)this).UserBanned);
            connection.On<RefereeAddedEvent>(nameof(IRefereeHubClient.RefereeAdded), ((IRefereeHubClient)this).RefereeAdded);
            connection.On<RefereeRemovedEvent>(nameof(IRefereeHubClient.RefereeRemoved), ((IRefereeHubClient)this).RefereeRemoved);
            connection.On<RefereeInvitedEvent>(nameof(IRefereeHubClient.RefereeInvited), ((IRefereeHubClient)this).RefereeInvited);
            connection.On<RoomSettingsChangedEvent>(nameof(IRefereeHubClient.RoomSettingsChanged), ((IRefereeHubClient)this).RoomSettingsChanged);
            connection.On<MatchStateChangedEvent>(nameof(IRefereeHubClient.MatchStateChanged), ((IRefereeHubClient)this).MatchStateChanged);
            connection.On<PlaylistItemAddedEvent>(nameof(IRefereeHubClient.PlaylistItemAdded), ((IRefereeHubClient)this).PlaylistItemAdded);
            connection.On<PlaylistItemChangedEvent>(nameof(IRefereeHubClient.PlaylistItemChanged), ((IRefereeHubClient)this).PlaylistItemChanged);
            connection.On<PlaylistItemRemovedEvent>(nameof(IRefereeHubClient.PlaylistItemRemoved), ((IRefereeHubClient)this).PlaylistItemRemoved);
            connection.On<RollCompletedEvent>(nameof(IRefereeHubClient.RollCompleted), ((IRefereeHubClient)this).RollCompleted);
            connection.On<UserStatusChangedEvent>(nameof(IRefereeHubClient.UserStatusChanged), ((IRefereeHubClient)this).UserStatusChanged);
            connection.On<UserModsChangedEvent>(nameof(IRefereeHubClient.UserModsChanged), ((IRefereeHubClient)this).UserModsChanged);
            connection.On<UserStyleChangedEvent>(nameof(IRefereeHubClient.UserStyleChanged), ((IRefereeHubClient)this).UserStyleChanged);
            connection.On<UserTeamChangedEvent>(nameof(IRefereeHubClient.UserTeamChanged), ((IRefereeHubClient)this).UserTeamChanged);
            connection.On<CountdownStartedEvent>(nameof(IRefereeHubClient.CountdownStarted), ((IRefereeHubClient)this).CountdownStarted);
            connection.On<CountdownStoppedEvent>(nameof(IRefereeHubClient.CountdownStopped), ((IRefereeHubClient)this).CountdownStopped);
            connection.On<MatchStartedEvent>(nameof(IRefereeHubClient.MatchStarted), ((IRefereeHubClient)this).MatchStarted);
            connection.On<MatchAbortedEvent>(nameof(IRefereeHubClient.MatchAborted), ((IRefereeHubClient)this).MatchAborted);
            connection.On<MatchCompletedEvent>(nameof(IRefereeHubClient.MatchCompleted), ((IRefereeHubClient)this).MatchCompleted);
            connection.On(nameof(IStatefulUserHubClient.DisconnectRequested), ((IStatefulUserHubClient)this).DisconnectRequested);
        };

        IsConnected.BindTo(connector.IsConnected);

        AccessToken.BindValueChanged(v =>
        {
            if (v.NewValue is not null)
            {
                connector.Reconnect().FireAndForget(
                    onError: ex => Debug.WriteLine($"Failed to reconnect to referee hub after access token change: {ex.Message}"));
            }
        }, true);
    }

    public override Task Ping(string message) => invoke(nameof(IRefereeHubServer.Ping), message);

    public override Task<RoomJoinedResponse> MakeRoom(MakeRoomRequest request)
        => invoke<RoomJoinedResponse>(nameof(IRefereeHubServer.MakeRoom), request);

    public override Task<RoomJoinedResponse> JoinRoom(long roomId)
        => invoke<RoomJoinedResponse>(nameof(IRefereeHubServer.JoinRoom), roomId);

    public override Task LeaveRoom(long roomId) => invoke(nameof(IRefereeHubServer.LeaveRoom), roomId);

    public override Task CloseRoom(long roomId) => invoke(nameof(IRefereeHubServer.CloseRoom), roomId);

    public override Task InvitePlayer(long roomId, int userId) => invoke(nameof(IRefereeHubServer.InvitePlayer), roomId, userId);

    public override Task KickPlayer(long roomId, int userId) => invoke(nameof(IRefereeHubServer.KickPlayer), roomId, userId);

    public override Task BanUser(long roomId, int bannedUserId) => invoke(nameof(IRefereeHubServer.BanUser), roomId, bannedUserId);

    public override Task AddReferee(long roomId, int targetUserId) => invoke(nameof(IRefereeHubServer.AddReferee), roomId, targetUserId);

    public override Task RemoveReferee(long roomId, int targetUserId) => invoke(nameof(IRefereeHubServer.RemoveReferee), roomId, targetUserId);

    public override Task ChangeRoomSettings(long roomId, ChangeRoomSettingsRequest request)
        => invoke(nameof(IRefereeHubServer.ChangeRoomSettings), roomId, request);

    public override Task EditCurrentPlaylistItem(long roomId, EditCurrentPlaylistItemRequest request)
        => invoke(nameof(IRefereeHubServer.EditCurrentPlaylistItem), roomId, request);

    public override Task AddPlaylistItem(long roomId, AddPlaylistItemRequest request)
        => invoke(nameof(IRefereeHubServer.AddPlaylistItem), roomId, request);

    public override Task EditPlaylistItem(long roomId, EditPlaylistItemRequest request)
        => invoke(nameof(IRefereeHubServer.EditPlaylistItem), roomId, request);

    public override Task RemovePlaylistItem(long roomId, RemovePlaylistItemRequest request)
        => invoke(nameof(IRefereeHubServer.RemovePlaylistItem), roomId, request);

    public override Task Roll(long roomId, Server.Spectator.Hubs.Referee.Models.Requests.RollRequest request)
        => invoke(nameof(IRefereeHubServer.Roll), roomId, request);

    public override Task MoveUser(long roomId, MoveUserRequest request)
        => invoke(nameof(IRefereeHubServer.MoveUser), roomId, request);

    public override Task SetLockState(long roomId, Server.Spectator.Hubs.Referee.Models.Requests.SetLockStateRequest request)
        => invoke(nameof(IRefereeHubServer.SetLockState), roomId, request);

    public override Task StartMatch(long roomId, StartGameplayRequest request)
        => invoke(nameof(IRefereeHubServer.StartMatch), roomId, request);

    public override Task StopMatchCountdown(long roomId) => invoke(nameof(IRefereeHubServer.StopMatchCountdown), roomId);

    public override Task AbortMatch(long roomId) => invoke(nameof(IRefereeHubServer.AbortMatch), roomId);

    protected override Task DisconnectInternal()
    {
        if (connector == null)
            return base.DisconnectInternal();

        return Task.WhenAll(base.DisconnectInternal(), connector.Disconnect());
    }

    private Task invoke(string methodName, params object?[] args)
    {
        if (!IsConnected.Value)
            return Task.CompletedTask;

        Debug.Assert(connection != null);
        return connection.InvokeCoreAsync(methodName, args);
    }

    private Task<T> invoke<T>(string methodName, params object?[] args)
    {
        if (!IsConnected.Value)
            return Task.FromCanceled<T>(new CancellationToken(true));

        Debug.Assert(connection != null);
        return connection.InvokeCoreAsync<T>(methodName, args);
    }

    private partial class CustomAuthencationHubClientConnector : HubClientConnector
    {
        private readonly string endpoint;
        public Bindable<string?> AccessToken { get; } = new Bindable<string?>();

        public CustomAuthencationHubClientConnector(string clientName, string endpoint, IAPIProvider api)
            // version hash is not required for referee hub
            : base(clientName, endpoint, api, versionHash: string.Empty)
        {
            this.endpoint = endpoint;
        }

        protected override Task<PersistentEndpointClient> BuildConnectionAsync(CancellationToken cancellationToken)
        {
            var builder = new HubConnectionBuilder()
                .WithUrl(endpoint, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(AccessToken.Value ?? API.AccessToken);
                });

            var newConnection = builder.Build();

            ConfigureConnection?.Invoke(newConnection);

            return Task.FromResult((PersistentEndpointClient)new HubClient(newConnection));
        }
    }
}
