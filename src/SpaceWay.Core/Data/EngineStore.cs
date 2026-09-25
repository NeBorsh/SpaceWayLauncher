using Dapper;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Data;

/// <summary>Records of downloaded engine builds and modules.</summary>
public sealed class EngineStore(LauncherDatabase db)
{
    public IReadOnlyList<InstalledEngine> GetEngines() => db.Connection
        .Query<InstalledEngine>("SELECT Version, Signature FROM InstalledEngine ORDER BY Version")
        .ToList();

    public InstalledEngine? FindEngine(string version) => db.Connection
        .QuerySingleOrDefault<InstalledEngine>(
            "SELECT Version, Signature FROM InstalledEngine WHERE Version = @version",
            new { version });

    public void AddEngine(InstalledEngine engine) => db.Connection.Execute(
        """
        INSERT INTO InstalledEngine (Version, Signature) VALUES (@Version, @Signature)
        ON CONFLICT (Version) DO UPDATE SET Signature = excluded.Signature
        """,
        engine);

    public void RemoveEngine(string version) => db.Connection.Execute(
        "DELETE FROM InstalledEngine WHERE Version = @version", new { version });

    public IReadOnlyList<InstalledEngineModule> GetModules() => db.Connection
        .Query<InstalledEngineModule>("SELECT Name, Version FROM InstalledEngineModule ORDER BY Name, Version")
        .ToList();

    public bool HasModule(string name, string version) => db.Connection.ExecuteScalar<long>(
        "SELECT COUNT(*) FROM InstalledEngineModule WHERE Name = @name AND Version = @version",
        new { name, version }) > 0;

    public void AddModule(InstalledEngineModule module) => db.Connection.Execute(
        """
        INSERT INTO InstalledEngineModule (Name, Version) VALUES (@Name, @Version)
        ON CONFLICT (Name, Version) DO NOTHING
        """,
        module);

    public void RemoveModule(InstalledEngineModule module) => db.Connection.Execute(
        "DELETE FROM InstalledEngineModule WHERE Name = @Name AND Version = @Version", module);
}
