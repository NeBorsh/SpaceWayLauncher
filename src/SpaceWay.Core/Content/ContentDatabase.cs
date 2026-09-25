using System.Diagnostics;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SpaceWay.Core.Data;

namespace SpaceWay.Core.Content;

/// <summary>
/// Database of downloaded content.
/// </summary>
public sealed class ContentDatabase(string? path = null, bool inMemory = false)
{
    private readonly string _path = path ?? LauncherPaths.PathContentDb;

    /// <summary>
    /// Prepares the database: creates the file, enables WAL, applies migrations.
    /// </summary>
    public void Initialize()
    {
        if (!inMemory && Path.GetDirectoryName(_path) is { Length: > 0 } dir)
            Directory.CreateDirectory(dir);

        using var connection = Open();

        if (!inMemory)
            connection.Execute("PRAGMA journal_mode=WAL");

        var stopwatch = Stopwatch.StartNew();
        Migrator.Migrate(connection, Migrator.ContentScripts);
        Log.Debug("Content database migrations took {Elapsed}", stopwatch.Elapsed);
    }

    /// <summary>Opens a connection. The caller must dispose it.</summary>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = inMemory ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,

            Cache = inMemory ? SqliteCacheMode.Shared : SqliteCacheMode.Default,

            Pooling = false,
            ForeignKeys = true,
        }.ToString());

        connection.Open();
        return connection;
    }

    /// <summary>In-memory database for tests.</summary>
    public static ContentDatabase CreateInMemory()
    {
        var db = new ContentDatabase($"content-{Guid.NewGuid():N}", inMemory: true);
        db.KeepAlive = db.Open();
        db.Initialize();
        return db;
    }

    /// <summary>
    /// Connection that keeps the in-memory database alive.
    /// </summary>
    public SqliteConnection? KeepAlive { get; private set; }
}
