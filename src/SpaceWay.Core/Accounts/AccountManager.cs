using Serilog;
using SpaceWay.Core.Data;

namespace SpaceWay.Core.Accounts;

/// <summary>
/// Accounts and their tokens.
/// </summary>
public sealed class AccountManager(
    AccountStore store,
    ITokenStore tokens,
    IAuthApi api,
    SettingsStore settings)
{
    private List<Account> _accounts = [];
    private List<AuthServer> _servers = [];

    public event Action? Changed;

    public IReadOnlyList<Account> Accounts => _accounts;

    public IReadOnlyList<AuthServer> AuthServers => _servers;

    /// <summary>Tokens are kept in OS-protected storage rather than plain text.</summary>
    public bool TokensAreSecure => tokens.IsSecure;

    /// <summary>Account used to launch the game.</summary>
    public Account? Selected
    {
        get
        {
            var saved = settings.Get(SettingKeys.SelectedAccount);
            if (saved == null)
                return _accounts.FirstOrDefault();

            return _accounts.FirstOrDefault(a => a.SecretKey == saved) ?? _accounts.FirstOrDefault();
        }
    }

    public void Reload()
    {
        _accounts = [.. store.GetAccounts()];
        _servers = [.. store.GetAuthServers()];
        Changed?.Invoke();
    }

    public void Select(Account account)
    {
        settings.Set(SettingKeys.SelectedAccount, account.SecretKey);
        Changed?.Invoke();
    }

    public AuthServer? FindServer(Guid id) => _servers.FirstOrDefault(s => s.Id == id);

    public void SaveServer(AuthServer server)
    {
        store.SaveAuthServer(server);
        Reload();
    }

    /// <summary>Number of accounts that will be removed along with the server.</summary>
    public int AccountsOn(Guid serverId) => _accounts.Count(a => a.AuthServerId == serverId);

    /// <summary>
    /// Removes an auth server together with its accounts and their tokens.
    /// </summary>
    public void RemoveServer(Guid id)
    {
        if (id == AuthServer.OfflineId)
            throw new InvalidOperationException("The offline pseudo-server cannot be removed");

        foreach (var account in _accounts.Where(a => a.AuthServerId == id))
        {
            tokens.Remove(account.SecretKey);
        }

        store.DeleteAuthServer(id);
        Reload();
    }

    /// <summary>
    /// Signs in and stores the account.
    /// </summary>
    public async Task<AuthResult> LoginAsync(
        AuthServer server,
        string username,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancel = default)
    {
        var result = await api.AuthenticateAsync(
            server.Address, username, password, twoFactorCode, cancel);

        if (!result.IsSuccess || result.Token == null)
            return result;

        var account = new Account(result.UserId, result.Username!, server.Id, result.Token.ExpiresAt);

        store.SaveAccount(account);
        tokens.Set(account.SecretKey, result.Token);

        Reload();
        settings.Set(SettingKeys.SelectedAccount, account.SecretKey);

        Log.Information("Signed in: {User} on {Server}", account.Username, server.DisplayName);
        return result;
    }

    /// <summary>
    /// Creates an account for playing without authentication.
    /// </summary>
    public Account CreateOfflineAccount(string username)
    {
        var account = new Account(
            Guid.NewGuid(),
            username.Trim(),
            AuthServer.OfflineId,
            DateTimeOffset.MaxValue);

        store.SaveAccount(account);
        Reload();
        settings.Set(SettingKeys.SelectedAccount, account.SecretKey);

        Log.Information("Created offline account {User}", account.Username);
        return account;
    }

    /// <summary>
    /// Signs out and revokes the token on the server.
    /// </summary>
    public async Task LogoutAsync(Account account, CancellationToken cancel = default)
    {
        var token = tokens.Get(account.SecretKey);
        var server = FindServer(account.AuthServerId);

        if (token != null && server is { IsOffline: false })
            await api.LogoutAsync(server.Address, token.Value, cancel);

        tokens.Remove(account.SecretKey);
        store.DeleteAccount(account.UserId, account.AuthServerId);

        Reload();
    }

    public AuthToken? GetToken(Account account) => tokens.Get(account.SecretKey);

    /// <summary>
    /// Returns a usable token, refreshing it if needed.
    /// Null means a full sign-in with a password is required.
    /// </summary>
    public async Task<AuthToken?> EnsureValidTokenAsync(
        Account account,
        CancellationToken cancel = default)
    {
        if (account.AuthServerId == AuthServer.OfflineId)
            return null;

        var token = tokens.Get(account.SecretKey);
        if (token == null)
            return null;

        var now = DateTimeOffset.UtcNow;

        if (token.IsExpired(now))
        {
            Log.Information("Token of {User} expired", account.Username);
            return null;
        }

        if (!token.NeedsRefresh(now))
            return token;

        var server = FindServer(account.AuthServerId);
        if (server == null)
            return token;

        try
        {
            var refreshed = await api.RefreshAsync(server.Address, token.Value, cancel);
            if (refreshed == null)
                return null;

            tokens.Set(account.SecretKey, refreshed);
            store.SaveAccount(account with { TokenExpiry = refreshed.ExpiresAt });
            Reload();

            return refreshed;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to refresh token of {User}", account.Username);
            return token;
        }
    }
}
