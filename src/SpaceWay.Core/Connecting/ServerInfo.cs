using System.Text.Json.Serialization;
using SpaceWay.Core.Content;

namespace SpaceWay.Core.Connecting;

/// <summary>
/// What the server reports about itself at <c>/info</c>.
/// </summary>
public sealed record ServerInfo
{
    /// <summary>
    /// Where the game connection should go.
    /// </summary>
    [JsonPropertyName("connect_address")]
    public string? ConnectAddress { get; init; }

    [JsonPropertyName("build")]
    public ServerBuildInformation? Build { get; init; }

    [JsonPropertyName("auth")]
    public ServerAuthInfo? Auth { get; init; }

    [JsonPropertyName("desc")]
    public string? Description { get; init; }

    /// <summary>Server links: Discord, wiki, website.</summary>
    [JsonPropertyName("links")]
    public ServerLink[]? Links { get; init; }

    [JsonPropertyName("privacy_policy")]
    public ServerPrivacyPolicy? PrivacyPolicy { get; init; }
}

/// <summary>How the server treats authentication.</summary>
public sealed record ServerAuthInfo
{
    [JsonPropertyName("mode")]
    public AuthMode Mode { get; init; }

    /// <summary>
    /// Public key of the auth server this server trusts.
    /// </summary>
    [JsonPropertyName("public_key")]
    public string? PublicKey { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthMode
{
    /// <summary>Allows players with and without authentication.</summary>
    Optional = 0,

    /// <summary>Authenticated players only.</summary>
    Required = 1,

    /// <summary>Does not use authentication at all.</summary>
    Disabled = 2,
}

/// <summary>
/// The server's privacy policy.
/// </summary>
public sealed record ServerPrivacyPolicy(
    [property: JsonPropertyName("link")] string Link,
    [property: JsonPropertyName("identifier")] string Identifier,
    [property: JsonPropertyName("version")] string Version);

/// <summary>
/// A link from the server description.
/// </summary>
public sealed record ServerLink(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("url")] string? Url);
