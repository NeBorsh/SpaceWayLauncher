namespace SpaceWay.Core.Accounts;

/// <summary>
/// An auth server. Unlike the official launcher, where the auth server is
/// global and set through an environment variable, each account is bound
/// to its own server, so accounts from several servers can coexist.
/// </summary>
public sealed record AuthServer(Guid Id, string DisplayName, Uri Address)
{
    public static readonly Guid OfficialId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Pseudo-server for playing without authentication.
    /// </summary>
    public static readonly Guid OfflineId = new("00000000-0000-0000-0000-000000000002");

    /// <summary>The official server. Present by default but not hardcoded; it can be removed.</summary>
    public static AuthServer Official { get; } = new(
        OfficialId, "Space Station 14", new Uri("https://auth.spacestation14.com/"));

    public static AuthServer Offline { get; } = new(
        OfflineId, "Offline", new Uri("offline://local"));

    /// <summary>
    /// Sign-in without authentication. The game server only allows it if it
    /// permits unauthenticated players, and others will see the name as unverified.
    /// </summary>
    public bool IsOffline => Id == OfflineId;

    /// <summary>
    /// Parses a user-entered auth server address. A missing scheme defaults to https.
    /// Plain http is only accepted for the local machine, since the password
    /// would otherwise travel unencrypted.
    /// </summary>
    public static AuthServerAddressProblem ParseAddress(string input, out Uri? address)
    {
        address = null;
        var trimmed = input.Trim();

        if (trimmed.Length == 0)
            return AuthServerAddressProblem.Empty;

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        if (!trimmed.EndsWith('/'))
            trimmed += "/";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host))
        {
            return AuthServerAddressProblem.Invalid;
        }

        if (uri.Scheme == "http" && !uri.IsLoopback)
            return AuthServerAddressProblem.Insecure;

        address = uri;
        return AuthServerAddressProblem.None;
    }
}

/// <summary>What is wrong with an auth server address.</summary>
public enum AuthServerAddressProblem
{
    None,
    Empty,
    Invalid,

    /// <summary>Plain http to a remote host.</summary>
    Insecure,
}
