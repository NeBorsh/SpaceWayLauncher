using Dapper;
using SpaceWay.Core.Accounts;

namespace SpaceWay.Core.Data;

public sealed class AccountStore(LauncherDatabase db)
{
    public IReadOnlyList<AuthServer> GetAuthServers() => db.Connection.Query<AuthServerRow>(
            "SELECT Id, DisplayName, Address FROM AuthServer ORDER BY DisplayName")
        .Select(r => r.ToEntry())
        .ToList();

    public void SaveAuthServer(AuthServer server) => db.Connection.Execute(
        """
        INSERT INTO AuthServer (Id, DisplayName, Address)
        VALUES (@Id, @DisplayName, @Address)
        ON CONFLICT (Id) DO UPDATE SET
            DisplayName = excluded.DisplayName,
            Address     = excluded.Address
        """,
        new { server.Id, server.DisplayName, Address = server.Address.AbsoluteUri });

    /// <summary>
    /// Deletes an auth server together with its accounts (cascade).
    /// </summary>
    public void DeleteAuthServer(Guid id) => db.Connection.Execute(
        "DELETE FROM AuthServer WHERE Id = @id", new { id });

    public IReadOnlyList<Account> GetAccounts() => db.Connection.Query<Account>(
        """
        SELECT UserId, Username, AuthServerId, TokenExpiry
        FROM Account
        ORDER BY Username
        """).ToList();

    public void SaveAccount(Account account) => db.Connection.Execute(
        """
        INSERT INTO Account (UserId, AuthServerId, Username, TokenExpiry)
        VALUES (@UserId, @AuthServerId, @Username, @TokenExpiry)
        ON CONFLICT (UserId, AuthServerId) DO UPDATE SET
            Username    = excluded.Username,
            TokenExpiry = excluded.TokenExpiry
        """,
        new
        {
            account.UserId,
            account.AuthServerId,
            account.Username,
            TokenExpiry = account.TokenExpiry.ToString("O"),
        });

    public void DeleteAccount(Guid userId, Guid authServerId) => db.Connection.Execute(
        "DELETE FROM Account WHERE UserId = @userId AND AuthServerId = @authServerId",
        new { userId, authServerId });

    public void SeedIfEmpty()
    {
        if (db.Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM AuthServer") > 0)
            return;

        SaveAuthServer(AuthServer.Official);
    }

    /// <summary>
    /// Creates the pseudo-server record for unauthenticated play if missing.
    /// </summary>
    public void EnsureOfflineServer()
    {
        var exists = db.Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM AuthServer WHERE Id = @id",
            new { id = AuthServer.OfflineId }) > 0;

        if (!exists)
            SaveAuthServer(AuthServer.Offline);
    }

    private sealed record AuthServerRow(Guid Id, string DisplayName, string Address)
    {
        public AuthServer ToEntry() => new(Id, DisplayName, new Uri(Address));
    }
}
