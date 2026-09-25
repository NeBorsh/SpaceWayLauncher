namespace SpaceWay.Core.Accounts;

/// <summary>
/// A player account. The token is not stored here; it lives in the OS
/// keystore and this record only references it.
/// </summary>
public sealed record Account(
    Guid UserId,
    string Username,
    Guid AuthServerId,
    DateTimeOffset TokenExpiry)
{
    /// <summary>
    /// Keystore entry key. UserId is only unique within a single
    /// auth server, so the key is composite.
    /// </summary>
    public string SecretKey => $"{AuthServerId:N}:{UserId:N}";
}
