using System.Diagnostics.CodeAnalysis;

namespace EzAuth.Interfaces;

public interface IEzAuthHttpClient : IDisposable
{
    public void Login(string username, string password);
    public string GetAccountRegistrationAddress(string? realmUrl = null);
    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content);
    public Task<string> GetStringAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri);
}
