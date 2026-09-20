using System.Diagnostics.CodeAnalysis;
using EzAuth.Interfaces;

namespace EzAuth.Keycloak;

public class KeyCloakHttpClient(EzAuthAddress address, Action<string> keyCloakRefreshTokenChanged, string? initialRefreshToken = null, HttpClient? client = null) : IEzAuthHttpClient
{
    public HttpClient client = client ?? new HttpClient();
    /// <summary>The caller supplied the HttpClient, so it stays the caller's to dispose.</summary>
    readonly bool ownsClient = client is null;
    /// <summary>Serializes login/refresh so the same refresh token is never redeemed twice at once.</summary>
    readonly SemaphoreSlim tokenLock = new(1, 1);
    string? currentAccessToken = null;
    DateTime accessTokenExpiry = DateTime.MinValue;
    string? currentRefreshToken = initialRefreshToken;
    DateTime refreshTokenExpiry = DateTime.MinValue;
    bool errorDuringTokenRetrieval = false;
    const int tokenRefreshBufferSeconds = 10;

    public void Login(string username, string password)
    {
        tokenLock.Wait();
        try
        {
            var res = EzKeycloak.I.LoginToCloak(client, address!.RealmUrl!, address.Client!, username, password);
            errorDuringTokenRetrieval = UpdateTokenVars(res);

            if (currentRefreshToken != null)
                keyCloakRefreshTokenChanged(currentRefreshToken);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    [Obsolete]
    public string GetAccountRegistrationAddress(string? realmUrl = null) => (realmUrl ?? address.RealmUrl) + "/account";

    public bool NewLogInNeeded()
    {
        if (currentRefreshToken == null || errorDuringTokenRetrieval || DateTime.Now >= refreshTokenExpiry)
            return true;
        return false;
    }

    public void RefreshTokenIfNeeded()
    {
        if (currentRefreshToken == null)
            return;

        if (DateTime.Now < accessTokenExpiry)
            return;

        // Only one refresh may be in flight: Keycloak rotates refresh tokens, and presenting an
        // already-rotated one counts as token reuse - it detaches the client session, i.e. it logs
        // the user out. Two threads must never redeem the same refresh token.
        tokenLock.Wait();
        try
        {
            // Another thread may have refreshed while we were waiting for the lock.
            if (DateTime.Now < accessTokenExpiry)
                return;

            var res = EzKeycloak.I.RefreshCloakSession(client, address!.RealmUrl!, address.Client!, currentRefreshToken);
            errorDuringTokenRetrieval = UpdateTokenVars(res);
            if (!errorDuringTokenRetrieval && currentRefreshToken != null)
                keyCloakRefreshTokenChanged(currentRefreshToken);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    bool UpdateTokenVars(LoginResponse? res)
    {
        if (res?.access_token == null || res.refresh_token == null)
            return true;
        if (res?.expires_in == null || res.refresh_expires_in == null)
            return true;
        currentAccessToken = res.access_token;
        accessTokenExpiry = DateTime.Now.AddSeconds((double)res.expires_in - tokenRefreshBufferSeconds);
        currentRefreshToken = res.refresh_token;
        refreshTokenExpiry = DateTime.Now.AddSeconds((double)res.refresh_expires_in - tokenRefreshBufferSeconds);
        return false;
    }

    /// <summary>
    /// Creates a request that carries the current access token in its own Authorization header.
    /// <para>
    /// The token must never be written to <see cref="HttpClient.DefaultRequestHeaders"/>: this class
    /// shares its HttpClient with the OIDC requests made by <see cref="EzKeycloak"/> (login/refresh),
    /// and a default header is applied to every request that does not define its own - that is how the
    /// access token ended up on the token endpoint, where Keycloak expects the client credentials
    /// (https://github.com/keycloak/keycloak/issues/47856).
    /// </para>
    /// </summary>
    HttpRequestMessage CreateAuthedRequest(HttpMethod method, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, requestUri) { Content = content };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {currentAccessToken}");
        return request;
    }

    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content)
    {
        RefreshTokenIfNeeded();
        return client.SendAsync(CreateAuthedRequest(HttpMethod.Post, requestUri, content));
    }

    public Task<HttpResponseMessage> PutAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content)
    {
        RefreshTokenIfNeeded();
        return client.SendAsync(CreateAuthedRequest(HttpMethod.Put, requestUri, content));
    }

    public Task<HttpResponseMessage> DeleteAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri)
    {
        RefreshTokenIfNeeded();
        return client.SendAsync(CreateAuthedRequest(HttpMethod.Delete, requestUri));
    }

    public Task<string> GetStringAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri)
    {
        // Deliberately not an async method: RefreshTokenIfNeeded() must throw synchronously at the
        // call site (as it always has) instead of handing the caller a faulted task.
        RefreshTokenIfNeeded();
        return SendGetStringAsync(CreateAuthedRequest(HttpMethod.Get, requestUri));
    }

    // ConfigureAwait(false) to match HttpClient.GetStringAsync, which does not capture the
    // caller's SynchronizationContext (callers use .Result from UI threads).
    async Task<string> SendGetStringAsync(HttpRequestMessage request)
    {
        using var response = await client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        // Only dispose what this instance created: an injected HttpClient may well be shared
        // (a DI singleton, a static client, or one the caller keeps using for other endpoints).
        if (ownsClient)
            client.Dispose();
        tokenLock.Dispose();
    }
}
