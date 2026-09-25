using Dapper;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Data;

public sealed class FavoritesStore(LauncherDatabase db)
{
    public IReadOnlyList<FavoriteServer> GetServers()
    {
        var servers = db.Connection.Query<ServerRow>(
            """
            SELECT Id, Address, ReportedName, CustomName, Note, SortOrder
            FROM FavoriteServer
            ORDER BY SortOrder, Address
            """).ToList();

        var tags = db.Connection.Query<(Guid ServerId, string Tag)>(
                "SELECT ServerId, Tag FROM FavoriteTag")
            .GroupBy(t => t.ServerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(t => t.Tag).ToList());

        return servers
            .Select(s => s.ToEntry(tags.GetValueOrDefault(s.Id, [])))
            .ToList();
    }

    public void SaveServer(FavoriteServer server)
    {
        using var transaction = db.Connection.BeginTransaction();

        db.Connection.Execute(
            """
            INSERT INTO FavoriteServer
                (Id, Address, AddressKey, ReportedName, CustomName, Note, SortOrder)
            VALUES
                (@Id, @Address, @AddressKey, @ReportedName, @CustomName, @Note, @SortOrder)
            ON CONFLICT (Id) DO UPDATE SET
                Address      = excluded.Address,
                AddressKey   = excluded.AddressKey,
                ReportedName = excluded.ReportedName,
                CustomName   = excluded.CustomName,
                Note         = excluded.Note,
                SortOrder    = excluded.SortOrder
            """,
            new
            {
                server.Id,
                server.Address,
                AddressKey = ServerAddress.Normalize(server.Address),
                server.ReportedName,
                server.CustomName,
                server.Note,
                server.SortOrder,
            },
            transaction);

        db.Connection.Execute(
            "DELETE FROM FavoriteTag WHERE ServerId = @id",
            new { id = server.Id },
            transaction);

        if (server.Tags.Count > 0)
        {
            db.Connection.Execute(
                "INSERT INTO FavoriteTag (ServerId, Tag) VALUES (@ServerId, @Tag)",
                server.Tags.Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(t => new { ServerId = server.Id, Tag = t }),
                transaction);
        }

        transaction.Commit();
    }

    public void DeleteServer(Guid id) => db.Connection.Execute(
        "DELETE FROM FavoriteServer WHERE Id = @id", new { id });

    /// <summary>Finds a favorite by address, regardless of its record.</summary>
    public FavoriteServer? FindByAddress(string address)
    {
        var key = ServerAddress.Normalize(address);
        var row = db.Connection.QuerySingleOrDefault<ServerRow>(
            """
            SELECT Id, Address, ReportedName, CustomName, Note, SortOrder
            FROM FavoriteServer WHERE AddressKey = @key
            """,
            new { key });

        if (row == null)
            return null;

        var tags = db.Connection.Query<string>(
            "SELECT Tag FROM FavoriteTag WHERE ServerId = @id", new { id = row.Id }).ToList();

        return row.ToEntry(tags);
    }

    /// <summary>
    /// Field types must match SQLite column types exactly:
    /// INTEGER is read as long, everything else as string.
    /// On mismatch Dapper rejects the record constructor entirely.
    /// </summary>

    private sealed record ServerRow(
        Guid Id,
        string Address,
        string? ReportedName,
        string? CustomName,
        string? Note,
        long SortOrder)
    {
        public FavoriteServer ToEntry(IReadOnlyList<string> tags) => new(Id, Address)
        {
            ReportedName = ReportedName,
            CustomName = CustomName,
            Note = Note,
            SortOrder = (int)SortOrder,
            Tags = tags,
        };
    }
}
