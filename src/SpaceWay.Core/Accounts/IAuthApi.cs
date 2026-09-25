namespace SpaceWay.Core.Accounts;

/// <summary>
/// Abstraction over the auth server, so account logic can be tested
/// without the network.
/// </summary>
public interface IAuthApi
{
    Task<AuthResult> AuthenticateAsync(
        Uri authServer,
        string username,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancel = default);

    Task<AuthToken?> RefreshAsync(Uri authServer, string token, CancellationToken cancel = default);

    Task LogoutAsync(Uri authServer, string token, CancellationToken cancel = default);

    Task<bool> CheckTokenAsync(Uri authServer, string token, CancellationToken cancel = default);
}
