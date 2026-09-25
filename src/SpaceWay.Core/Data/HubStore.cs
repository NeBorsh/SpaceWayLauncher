using Dapper;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Data;

public sealed class HubStore(LauncherDatabase db)
{
    /// <summary>Hubs in priority order.</summary>
    public IReadOnlyList<HubEntry> GetAll() => db.Connection.Query<HubRow>(
            "SELECT Id, DisplayName, Address, Priority, Enabled FROM Hub ORDER BY Priority, DisplayName")
        .Select(r => r.ToEntry())
        .ToList();

    public void Save(HubEntry hub) => db.Connection.Execute(
        """
        INSERT INTO Hub (Id, DisplayName, Address, Priority, Enabled)
        VALUES (@Id, @DisplayName, @Address, @Priority, @Enabled)
        ON CONFLICT (Id) DO UPDATE SET
            DisplayName = excluded.DisplayName,
            Address     = excluded.Address,
            Priority    = excluded.Priority,
            Enabled     = excluded.Enabled
        """,
        new
        {
            hub.Id,
            hub.DisplayName,
            Address = hub.Address.AbsoluteUri,
            hub.Priority,
            hub.Enabled,
        });

    public void Delete(Guid id) => db.Connection.Execute("DELETE FROM Hub WHERE Id = @id", new { id });

    /// <summary>
    /// Seeds the hub list with the official hub if it is empty.
    /// </summary>
    public void SeedIfEmpty()
    {
        if (db.Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM Hub") > 0)
            return;

        Save(HubEntry.Official);
    }

    /// <summary>
    /// Field types must match SQLite column types exactly:
    /// INTEGER is read as long, everything else as string.
    /// On mismatch Dapper rejects the record constructor entirely,
    /// even for obvious narrowing (long to int) or with a type handler.
    /// </summary>
    private sealed record HubRow(Guid Id, string DisplayName, string Address, long Priority, long Enabled)
    {
        public HubEntry ToEntry() => new(Id, DisplayName, new Uri(Address), (int)Priority, Enabled != 0);
    }
}
