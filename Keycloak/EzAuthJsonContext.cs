using System.Text.Json.Serialization;

namespace EzAuth.Keycloak;

/// <summary>Source-generated JSON metadata for Keycloak responses (reflection-free, AOT-safe).</summary>
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(UserinfoResponse))]
public partial class EzAuthJsonContext : JsonSerializerContext
{
}
