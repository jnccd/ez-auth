namespace EzAuth.Interfaces;

public class EzAuthException(string message) : Exception(message);

public interface IEzAuth
{
    public bool IsTokenValid(HttpClient client, string url, string accessToken, out EzAuthUserInfo? userInfo);
    public EzAuthLoginTokens? Login(HttpClient client, string url, string clientId, string username, string password);
    public EzAuthLoginTokens? RefreshSession(HttpClient client, string url, string clientId, string refreshToken);
}
