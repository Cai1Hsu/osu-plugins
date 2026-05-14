using System.Net.Sockets;
using AccessItEasy;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Online.API;
using OsuOAuth = osu.Game.Online.API.OAuth;

namespace osu.Plugin.Patches.RefereeHub;

public partial class OAuth
{
    public readonly OsuOAuth OsuOAuth;
    private readonly string endpoint;
    private readonly string clientId;
    private readonly string clientSecret;
    private readonly string scope;

    public Bindable<OAuthToken> Token => OsuOAuth.Token;

    public string TokenString
    {
        get => OsuOAuth.TokenString;
        set => OsuOAuth.TokenString = value;
    }

    public OAuth(string? clientId, string? clientSecret, string endpoint, string scope)
    {
        this.clientId = clientId ?? string.Empty;
        this.clientSecret = clientSecret ?? string.Empty;
        this.endpoint = endpoint;
        this.scope = scope;

        OsuOAuth = CreateOsuOAuth(this.clientId, this.clientSecret, this.endpoint);
    }

    public void AuthenticateWithCodeGrant(string code)
    {
        var codeGrantRequest = new AccessTokenRequestCodeGrant(code)
        {
            Url = $@"{endpoint}/oauth/token",
            Method = HttpMethod.Post,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scope = scope
        };

        using (codeGrantRequest)
        {
            try
            {
                codeGrantRequest.Perform();
            }
            catch (Exception ex)
            {
                OsuOAuth.Token.Value = null;

                var throwableException = ex;

                try
                {
                    // attempt to decode a displayable error string.
                    var error = JsonConvert.DeserializeObject<OAuthError>(codeGrantRequest.GetResponseString() ?? string.Empty);
                    if (error != null)
                        throwableException = new APIException(error.UserDisplayableError, ex, codeGrantRequest.ResponseStatusCode);
                }
                catch
                {
                }

                throw throwableException;
            }

            OsuOAuth.Token.Value = codeGrantRequest.ResponseObject;
        }
    }

    public bool AuthenticateWithRefresh(string refresh)
    {
        try
        {
            var refreshRequest = new AccessTokenRequestRefresh(refresh)
            {
                Url = $@"{endpoint}/oauth/token",
                Method = HttpMethod.Post,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Scope = scope
            };

            using (refreshRequest)
            {
                refreshRequest.Perform();

                Token.Value = refreshRequest.ResponseObject;
                return true;
            }
        }
        catch (SocketException)
        {
            // Network failure.
            return false;
        }
        catch (HttpRequestException)
        {
            // Network failure.
            return false;
        }
        catch
        {
            // Force a full re-authentication.
            Token.Value = null!;
            return false;
        }
    }

    public string RequestAccessToken() => RequestAccessToken(OsuOAuth);

    private class AccessTokenRequestRefresh : AccessTokenRequest
    {
        internal readonly string RefreshToken;

        internal AccessTokenRequestRefresh(string refreshToken)
        {
            RefreshToken = refreshToken;
            GrantType = @"refresh_token";
        }

        protected override void PrePerform()
        {
            AddParameter("refresh_token", RefreshToken);

            base.PrePerform();
        }
    }

    public void Clear() => Clear(OsuOAuth);

    private class AccessTokenRequestCodeGrant : AccessTokenRequest
    {
        private readonly string Code;

        public AccessTokenRequestCodeGrant(string code)
        {
            GrantType = "authorization_code";
            Code = code;
        }

        protected override void PrePerform()
        {
            AddParameter("code", Code);

            base.PrePerform();
        }
    }

    private abstract class AccessTokenRequest : OsuJsonWebRequest<OAuthToken>
    {
        protected string GrantType { get; init; } = null!;
        internal string ClientId { get; init; } = null!;
        internal string ClientSecret { get; init; } = null!;
        internal string Scope { get; init; } = null!;

        protected override void PrePerform()
        {
            AddParameter("grant_type", GrantType);
            AddParameter("client_id", ClientId);
            AddParameter("client_secret", ClientSecret);
            AddParameter("scope", Scope);

            base.PrePerform();
        }
    }

    private class OAuthError
    {
        public string UserDisplayableError => !string.IsNullOrEmpty(Hint) ? Hint : ErrorIdentifier;

        [JsonProperty("error")]
        public string ErrorIdentifier { get; set; } = null!;

        [JsonProperty("hint")]
        public string Hint { get; set; } = null!;

        [JsonProperty("message")]
        public string Message { get; set; } = null!;
    }

    #region Accessors

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    internal static extern OsuOAuth CreateOsuOAuth(string clientId, string clientSecret, string endpoint);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(RequestAccessToken))]
    internal static extern string RequestAccessToken(OsuOAuth instance);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(Clear))]
    internal static extern void Clear(OsuOAuth instance);

    #endregion
}