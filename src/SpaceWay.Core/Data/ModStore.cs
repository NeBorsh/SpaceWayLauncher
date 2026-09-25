using Dapper;

namespace SpaceWay.Core.Data;

/// <summary>
/// Mods enabled by the player.
/// </summary>
public sealed class ModStore(LauncherDatabase db)
{
    /// <summary>Enabled mods in load order.</summary>
    public IReadOnlyList<string> GetEnabled() => db.Connection
        .Query<string>("SELECT FileName FROM EnabledMod ORDER BY SortOrder")
        .ToList();

    public void Enable(string fileName) => db.Connection.Execute(
        """
        INSERT INTO EnabledMod (FileName, SortOrder)
        VALUES (@fileName, (SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM EnabledMod))
        ON CONFLICT (FileName) DO NOTHING
        """,
        new { fileName });

    public void Disable(string fileName) => db.Connection.Execute(
        "DELETE FROM EnabledMod WHERE FileName = @fileName", new { fileName });
}
