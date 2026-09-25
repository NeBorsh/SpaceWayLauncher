using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace SpaceWay.Core.Accounts;

/// <summary>
/// Client for the SS14 auth server.
/// </summary>
public sealed class AuthApi(HttpClient http) : IAuthApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<AuthResult> AuthenticateAsync(
        Uri authServer,
        string username,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancel = default)
    {
        var request = new AuthenticateRequest(username, password, twoFactorCode);

        try
        {
            using var response = await http.PostAsJsonAsync(
                new Uri(authServer, "api/auth/authenticate"), request, JsonOptions, cancel);

            if (response.IsSuccessStatusCode)
            {
                var success = await response.Content.ReadFromJsonAsync<AuthenticateResponse>(
                    JsonOptions, cancel);

                if (success == null)
                    return AuthResult.Failure(AuthDenyReason.UnknownError, "Empty server response");

                return AuthResult.Success(new AuthToken(success.Token, success.ExpireTime),
                    success.UserId, success.Username);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var deny = await response.Content.ReadFromJsonAsync<AuthenticateDenyResponse>(
                    JsonOptions, cancel);

                return AuthResult.Failure(deny?.Code ?? AuthDenyReason.UnknownError, deny?.Errors ?? []);
            }

            Log.Error("Auth server responded with code {Code}", response.StatusCode);
            return AuthResult.Failure(AuthDenyReason.UnknownError, $"Response code{(int)response.StatusCode}");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Log.Error(e, "Failed to reach auth server {Server}", authServer);
            return AuthResult.Failure(AuthDenyReason.ConnectionError, e.Message);
        }
        catch (JsonException e)
        {
            Log.Error(e, "Auth server sent an unparseable response");
            return AuthResult.Failure(AuthDenyReason.UnknownError, "Malformed server response");
        }
    }

    /// <summary>
    /// Refreshes a token. Null means the token is no longer valid
    /// and a full sign-in is required.
    /// </summary>
    public async Task<AuthToken?> RefreshAsync(
        Uri authServer,
        string token,
        CancellationToken cancel = default)
    {
        using var response = await http.PostAsJsonAsync(
            new Uri(authServer, "api/auth/refresh"), new RefreshRequest(token), JsonOptions, cancel);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log.Information("Token expired, cannot refresh");
            return null;
        }

        response.EnsureSuccessStatusCode();

        var refreshed = await response.Content.ReadFromJsonAsync<RefreshResponse>(JsonOptions, cancel);
        return refreshed == null ? null : new AuthToken(refreshed.NewToken, refreshed.ExpireTime);
    }

    /// <summary>
    /// Revokes a token on the server.
    /// </summary>
    public async Task LogoutAsync(Uri authServer, string token, CancellationToken cancel = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(
                new Uri(authServer, "api/auth/logout"), new LogoutRequest(token), JsonOptions, cancel);

            if (!response.IsSuccessStatusCode)
                Log.Warning("Server did not revoke the token, code {Code}", response.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Log.Warning(e, "Failed to revoke the token on the server");
        }
    }

    /// <summary>Checks whether a token is valid.</summary>
    public async Task<bool> CheckTokenAsync(
        Uri authServer,
        string token,
        CancellationToken cancel = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(authServer, "api/auth/ping"));
        request.Headers.Authorization = new AuthenticationHeaderValue("SS14Auth", token);

        using var response = await http.SendAsync(request, cancel);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    private sealed record AuthenticateRequest(
        string Username,
        string Password,
        [property: JsonPropertyName("tfaCode")] string? TfaCode);

    private sealed record AuthenticateResponse(
        string Token,
        string Username,
        Guid UserId,
        DateTimeOffset ExpireTime);

    private sealed record AuthenticateDenyResponse(string[] Errors, AuthDenyReason Code);

    private sealed record RefreshRequest(string Token);

    private sealed record RefreshResponse(string NewToken, DateTimeOffset ExpireTime);

    private sealed record LogoutRequest(string Token);
}

/// <param name="ExpiresAt">Expiration time. Tokens live for about a month.</param>
public sealed record AuthToken(string Value, DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Refresh early rather than at the last moment: a player may not open the
    /// launcher for weeks, and an expired token would require entering the password.
    /// </summary>
    public static readonly TimeSpan RefreshThreshold = TimeSpan.FromDays(15);

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool NeedsRefresh(DateTimeOffset now) => ExpiresAt - now <= RefreshThreshold;
}

/// <summary>Why the server rejected the sign-in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthDenyReason
{
    None = 0,
    InvalidCredentials = 1,
    AccountUnconfirmed = 2,
    TfaRequired = 3,
    TfaInvalid = 4,
    AccountLocked = 5,

    UnknownError = -1,
    ConnectionError = -2,
}

public sealed record AuthResult
{
    private AuthResult()
    {
    }

    public bool IsSuccess { get; private init; }

    public AuthToken? Token { get; private init; }

    public Guid UserId { get; private init; }

    public string? Username { get; private init; }

    public AuthDenyReason Reason { get; private init; }

    public IReadOnlyList<string> Errors { get; private init; } = [];

    /// <summary>The server requires a two-factor authentication code.</summary>
    public bool NeedsTwoFactor => Reason is AuthDenyReason.TfaRequired or AuthDenyReason.TfaInvalid;

    public static AuthResult Success(AuthToken token, Guid userId, string username) => new()
    {
        IsSuccess = true,
        Token = token,
        UserId = userId,
        Username = username,
    };

    public static AuthResult Failure(AuthDenyReason reason, params string[] errors) => new()
    {
        IsSuccess = false,
        Reason = reason,
        Errors = errors,
    };

    public static AuthResult Failure(AuthDenyReason reason, IReadOnlyList<string> errors) => new()
    {
        IsSuccess = false,
        Reason = reason,
        Errors = errors,
    };
}
