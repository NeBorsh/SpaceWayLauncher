using SpaceWay.Core.Accounts;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Fakes for account handling.
/// </summary>
internal sealed class FakeAuthApi : IAuthApi
{
    public AuthDenyReason? Deny { get; set; }

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public bool RefreshCalled { get; private set; }

    public bool RefreshThrows { get; set; }

    public bool LogoutCalled { get; private set; }

    public Task<AuthResult> AuthenticateAsync(
        Uri authServer,
        string username,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancel = default)
    {
        if (Deny is { } reason)
            return Task.FromResult(AuthResult.Failure(reason, "отказано"));

        var token = new AuthToken("токен", DateTimeOffset.UtcNow + TokenLifetime);
        return Task.FromResult(AuthResult.Success(token, Guid.NewGuid(), username));
    }

    public Task<AuthToken?> RefreshAsync(Uri authServer, string token, CancellationToken cancel = default)
    {
        RefreshCalled = true;

        if (RefreshThrows)
            throw new HttpRequestException("server unavailable");

        return Task.FromResult<AuthToken?>(
            new AuthToken("продлённый", DateTimeOffset.UtcNow.AddDays(30)));
    }

    public Task LogoutAsync(Uri authServer, string token, CancellationToken cancel = default)
    {
        LogoutCalled = true;
        return Task.CompletedTask;
    }

    public Task<bool> CheckTokenAsync(Uri authServer, string token, CancellationToken cancel = default) =>
        Task.FromResult(true);
}

internal sealed class FakeTokenStore : ITokenStore
{
    private readonly Dictionary<string, AuthToken> _tokens = [];

    public bool IsSecure => true;

    public AuthToken? Get(string key) => _tokens.GetValueOrDefault(key);

    public void Set(string key, AuthToken token) => _tokens[key] = token;

    public void Remove(string key) => _tokens.Remove(key);
}
