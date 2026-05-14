using AccessItEasy;
using Microsoft.AspNetCore.SignalR.Client;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API;

namespace osu.Plugin.Patches.RefereeHub;

public partial class RefereeHubConnector : HubClientConnector
{
    private readonly string endpoint;
    private readonly CustomAPIAccess apiAccess;

    public RefereeHubConnector(string endpoint, CustomAPIAccess apiAccess, IAPIProvider api)
        : base(nameof(RefereeHubConnector), endpoint, api, string.Empty)
    {
        this.endpoint = endpoint;
        this.apiAccess = apiAccess;

        // apiState is used to control connection state of the hub client.
        // however, RefereeHub uses a different authentication mechanism than the default API provider, 
        // so we need to manually bind the API state to the hub client's connection state.

        var apiStateBindable = GetApiState(this);
        apiStateBindable.UnbindBindings();
        apiStateBindable.BindTo(apiAccess.State);
    }

    protected override Task<PersistentEndpointClient> BuildConnectionAsync(CancellationToken cancellationToken)
    {
        var builder = new HubConnectionBuilder()
                .WithUrl(endpoint, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(apiAccess.AccessToken);
                });

        var newConnection = builder.Build();

        ConfigureConnection?.Invoke(newConnection);

        return Task.FromResult((PersistentEndpointClient)new HubClient(newConnection));
    }

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "apiState")]
    private static extern IBindable<APIState> GetApiState(PersistentEndpointClientConnector instance);
}
