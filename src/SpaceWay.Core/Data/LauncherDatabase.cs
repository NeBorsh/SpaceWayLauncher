using Dapper;
using Microsoft.Data.Sqlite;

namespace SpaceWay.Core.Data;

/// <summary>
/// Owns the launcher database connection.
/// </summary>
public sealed class LauncherDatabase : IDisposable
{
    public LauncherDatabase(string path)
    {
        TypeHandlers.Register();

        if (Path.GetDirectoryName(path) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);

        Connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());

        Connection.Open();

        Connection.Execute("PRAGMA foreign_keys = ON");

        Migrator.Migrate(Connection);
    }

    public SqliteConnection Connection { get; }

    /// <summary>In-memory database for tests.</summary>
    public static LauncherDatabase CreateInMemory() => new(":memory:");

    public void Dispose() => Connection.Dispose();
}
