namespace SpaceWay.Core.Hubs;

/// <summary>
/// Server address normalization.
/// </summary>
public static class ServerAddress
{
    public const int DefaultPort = 1212;

    /// <summary>Regular server address.</summary>
    public const string SchemeInsecure = "ss14";

    /// <summary>Server address whose HTTP endpoint is behind TLS.</summary>
    public const string SchemeSecure = "ss14s";

    /// <summary>
    /// Normalizes an address for comparison.
    /// An unparseable address is returned as is, trimmed and lowercased:
    /// better to show an odd server than to lose it.
    /// </summary>
    public static string Normalize(string address)
    {
        var trimmed = address.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return trimmed.ToLowerInvariant().TrimEnd('/');

        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort || uri.Port <= 0 ? DefaultPort : uri.Port;
        var path = uri.AbsolutePath.TrimEnd('/');

        return $"{uri.Scheme.ToLowerInvariant()}://{host}:{port}{path}";
    }

    /// <summary>
    /// Parses a server address.
    /// </summary>
    public static bool TryParse(string address, out Uri uri)
    {
        var trimmed = address.Trim();

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = $"{SchemeInsecure}://{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            || parsed.Scheme is not (SchemeInsecure or SchemeSecure)
            || string.IsNullOrWhiteSpace(parsed.Host))
        {
            uri = null!;
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>
    /// URL where the server reports about itself.
    /// </summary>
    public static Uri ApiAddress(Uri serverAddress)
    {
        var builder = new UriBuilder(serverAddress)
        {
            Scheme = serverAddress.Scheme == SchemeSecure ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
        };

        if (serverAddress.IsDefaultPort && serverAddress.Scheme == SchemeInsecure)
            builder.Port = DefaultPort;

        if (!builder.Path.EndsWith('/'))
            builder.Path += "/";

        return builder.Uri;
    }

    /// <summary>Server details: build, authentication, policy.</summary>
    public static Uri InfoAddress(Uri serverAddress) => new(ApiAddress(serverAddress), "info");

    /// <summary>Current server state: players, round.</summary>
    public static Uri StatusAddress(Uri serverAddress) => new(ApiAddress(serverAddress), "status");
}
