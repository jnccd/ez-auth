using System.Diagnostics.CodeAnalysis;

namespace EzAuth.Interfaces;

interface IEzAuthHttpClient : IDisposable
{
    public void Login(string username, string password);
    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content);
    public Task<string> GetStringAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri);
}
