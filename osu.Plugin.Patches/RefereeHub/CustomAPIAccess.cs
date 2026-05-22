using System.Runtime.ExceptionServices;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API;

namespace osu.Plugin.Patches.RefereeHub;

public partial class CustomAPIAccess : IDisposable
{
    private OAuth authentication = null!;
    private readonly Bindable<APIState> apiState = new Bindable<APIState>();

    public IBindable<APIState> State => apiState;

    public string AccessToken => authentication?.RequestAccessToken() ?? string.Empty;

    protected bool HasLogin => authentication?.Token.Value != null;

    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    private CancellationTokenSource? refreshCancellation;

    public readonly IBindable<string> ClientId = new Bindable<string>();
    public readonly IBindable<string> ClientSecret = new Bindable<string>();
    public readonly Bindable<string> TokenString = new Bindable<string>();

    private readonly EndpointConfiguration endpoint;
    private readonly string scope;

    public CustomAPIAccess(EndpointConfiguration endpoint, string scope)
    {
        this.endpoint = endpoint;
        this.scope = scope;

        ClientId.BindValueChanged(_ => recreateOAuth());
        ClientSecret.BindValueChanged(_ => recreateOAuth());
    }

    private void recreateOAuth()
    {
        refreshCancellation?.Cancel();
        refreshCancellation = new CancellationTokenSource();
        var cancellationToken = refreshCancellation.Token;

        string? oldToken = authentication?.TokenString ?? TokenString.Value;

        if (authentication != null)
            authentication.Token.ValueChanged -= onTokenChanged;

        authentication = new OAuth(ClientId.Value, ClientSecret.Value, endpoint.APIUrl, scope);

        if (!string.IsNullOrEmpty(oldToken))
        {
            try
            {
                authentication.TokenString = oldToken;

                if (authentication.Token.Value != null)
                {
                    // Copy the token so it isn't lost if authentication changes
                    var tokenToRefresh = authentication.Token.Value;
                    Task.Factory.StartNew(() => AuthenticateWithRefreshToken(tokenToRefresh, cancellationToken), cancellationToken);
                }
            }
            catch
            {
            }
        }

        authentication.Token.ValueChanged += onTokenChanged;

        updateState();
    }

    private void onTokenChanged(ValueChangedEvent<OAuthToken> token)
    {
        TokenString.Value = authentication.TokenString;
        updateState();
    }

    private void updateState()
    {
        apiState.Value = HasLogin ? APIState.Online : APIState.Offline;
    }

    public string CodeGrantUrl => $"{endpoint.WebsiteUrl}/oauth/authorize?client_id={ClientId.Value}&response_type=code&scope={scope}";

    public void Logout()
    {
        if (authentication != null)
            authentication.Clear();

        updateState();
    }

    public void AuthenticateWithRefreshToken(OAuthToken token, CancellationToken cancellationToken = default)
    {
        if (authentication == null || cancellationToken.IsCancellationRequested)
            return;

        if (string.IsNullOrEmpty(ClientId.Value) || string.IsNullOrEmpty(ClientSecret.Value))
            return;

        apiState.Value = APIState.Connecting;

        try
        {
            authentication.AuthenticateWithRefresh(token.RefreshToken);
        }
        catch { }

        if (!cancellationToken.IsCancellationRequested)
            updateState();
    }

    public async Task AuthenticateWithCodeGrant(string code)
    {
        if (authentication == null)
            return;

        if (string.IsNullOrEmpty(ClientId.Value) || string.IsNullOrEmpty(ClientSecret.Value))
            throw new InvalidOperationException("Client Id and Client Secret must not be empty.");

        apiState.Value = APIState.Connecting;

        try
        {
            await Task.Factory.StartNew(
                () => authentication.AuthenticateWithCodeGrant(code), TaskCreationOptions.LongRunning);
        }
        catch (AggregateException ae)
        {
            if (ae.InnerException != null)
                ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
            throw;
        }
        finally
        {
            updateState();
        }
    }

    public virtual void Dispose()
    {
        refreshCancellation?.Cancel();
        cancellationTokenSource.Cancel();
    }
}
