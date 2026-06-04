using System.Diagnostics.CodeAnalysis;

namespace EzAuth.Interfaces;

public interface IEzAuthHttpClient : IDisposable
{
    public void Login(string username, string password);
    /// <summary>
    /// Use IEzAuth.GetAccountRegistrationAddress() instead, the realmUrl should come from the ui user input. 
    /// This is for new registrations and the IEzAuthHttpClient only contains already working connections which makes this useless for registrations.
    /// </summary>
    [Obsolete]
    public string GetAccountRegistrationAddress(string? realmUrl = null);
    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content);
    public Task<HttpResponseMessage> PutAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content);
    public Task<HttpResponseMessage> DeleteAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri);
    public Task<string> GetStringAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri);
}
