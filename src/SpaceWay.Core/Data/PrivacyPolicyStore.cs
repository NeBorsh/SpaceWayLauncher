using Dapper;

namespace SpaceWay.Core.Data;

/// <summary>Player consent to server privacy policies.</summary>
public sealed class PrivacyPolicyStore(LauncherDatabase db)
{
    /// <summary>
    /// Policy version the player accepted.
    /// </summary>
    /// <returns>null if never accepted.</returns>
    public string? AcceptedVersion(string identifier) => db.Connection.QuerySingleOrDefault<string>(
        "SELECT Version FROM AcceptedPrivacyPolicy WHERE Identifier = @identifier",
        new { identifier });

    public void Accept(string identifier, string version) => db.Connection.Execute(
        """
        INSERT INTO AcceptedPrivacyPolicy (Identifier, Version, AcceptedAt, LastConnected)
        VALUES (@identifier, @version, datetime('now'), datetime('now'))
        ON CONFLICT (Identifier) DO UPDATE SET
            Version       = excluded.Version,
            AcceptedAt    = excluded.AcceptedAt,
            LastConnected = excluded.LastConnected
        """,
        new { identifier, version });

    /// <summary>Records another connection to a server with this policy.</summary>
    public void Touch(string identifier) => db.Connection.Execute(
        "UPDATE AcceptedPrivacyPolicy SET LastConnected = datetime('now') WHERE Identifier = @identifier",
        new { identifier });

    /// <summary>Forgets consent. The next connection will ask again.</summary>
    public void Forget(string identifier) => db.Connection.Execute(
        "DELETE FROM AcceptedPrivacyPolicy WHERE Identifier = @identifier", new { identifier });
}
