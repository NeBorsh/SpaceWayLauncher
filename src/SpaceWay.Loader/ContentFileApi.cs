using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Robust.LoaderApi;
using SpaceWay.Vendor.ZStd;

namespace SpaceWay.Loader;

/// <summary>
/// Serves game files to the engine directly from the content database.
/// </summary>
internal sealed class ContentFileApi : IFileApi, IDisposable
{
    private readonly Dictionary<string, FileEntry> _files = new();
    private readonly ConcurrentBag<Reader> _readers = [];
    private readonly SemaphoreSlim _available;
    private readonly int _poolSize;
    private readonly SqliteConnection _main;
    private readonly SqliteTransaction _mainTransaction;

    public ContentFileApi(string databasePath, long versionId)
    {
        _poolSize = PoolSize();

        _main = Open(databasePath, readOnly: false);

        LoadManifest(_main, transaction: null, versionId);

        RegisterRunningClient(_main, versionId);

        _mainTransaction = _main.BeginTransaction(deferred: true);

        _readers.Add(new Reader(_main, _mainTransaction, OwnsConnection: false));

        for (var i = 1; i < _poolSize; i++)
        {
            var connection = Open(databasePath, readOnly: true);
            _readers.Add(new Reader(connection, connection.BeginTransaction(deferred: true), OwnsConnection: true));
        }

        _available = new SemaphoreSlim(_poolSize, _poolSize);
    }

    public IEnumerable<string> AllFiles => _files.Keys;

    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        if (!_files.TryGetValue(path, out var entry))
        {
            stream = null;
            return false;
        }

        _available.Wait();
        Reader? reader = null;
        try
        {
            if (!_readers.TryTake(out reader))
                throw new InvalidOperationException("A free connection was promised but none is available");

            stream = Read(reader.Connection, entry);
            return true;
        }
        finally
        {
            if (reader != null)
                _readers.Add(reader);

            _available.Release();
        }
    }

    public void Dispose()
    {
        for (var i = 0; i < _poolSize; i++)
        {
            _available.Wait();

            if (!_readers.TryTake(out var reader))
                continue;

            reader.Transaction.Dispose();

            if (reader.OwnsConnection)
                reader.Connection.Dispose();
        }

        UnregisterRunningClient(_main);

        _mainTransaction.Dispose();
        _main.Dispose();
        _available.Dispose();
    }

    private static Stream Read(SqliteConnection connection, FileEntry entry)
    {
        var buffer = GC.AllocateUninitializedArray<byte>(entry.Size);

        using var blob = new SqliteBlob(connection, "Content", "Data", entry.ContentId, readOnly: true);

        switch (entry.Compression)
        {
            case ContentCompression.None:
                blob.ReadExactly(buffer);
                break;

            case ContentCompression.Deflate:
                CopyExactly(new DeflateStream(blob, CompressionMode.Decompress), buffer);
                break;

            case ContentCompression.ZStd:
                CopyExactly(new ZStdDecompressStream(blob), buffer);
                break;

            default:
                throw new NotSupportedException($"Unknown compression:{entry.Compression}");
        }

        return new MemoryStream(buffer, writable: false);
    }

    /// <summary>
    /// Decompresses exactly as much as the manifest promised.
    /// </summary>
    private static void CopyExactly(Stream source, byte[] buffer)
    {
        using (source)
        {
            source.ReadExactly(buffer);
        }
    }

    private void LoadManifest(SqliteConnection connection, SqliteTransaction? transaction, long versionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT m.Path, c.Id, c.Size, c.Compression
            FROM ContentManifest m
            INNER JOIN Content c ON c.Id = m.ContentId
            WHERE m.VersionId = $versionId
            """;
        command.Parameters.AddWithValue("$versionId", versionId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            _files[reader.GetString(0)] = new FileEntry(
                reader.GetInt64(1),
                reader.GetInt32(2),
                (ContentCompression)reader.GetInt32(3));
        }

        if (_files.Count == 0)
            throw new InvalidOperationException($"Content version {versionId} is empty or missing");
    }

    /// <summary>
    /// Registers in the database as a running client.
    /// </summary>
    private static void RegisterRunningClient(SqliteConnection connection, long versionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RunningClient (ProcessId, MainModule, UsedVersion)
            VALUES ($processId, $mainModule, $versionId)
            ON CONFLICT (ProcessId) DO UPDATE SET
                MainModule = excluded.MainModule,
                UsedVersion = excluded.UsedVersion
            """;
        command.Parameters.AddWithValue("$processId", Environment.ProcessId);
        command.Parameters.AddWithValue("$mainModule", MainModulePath());
        command.Parameters.AddWithValue("$versionId", versionId);
        command.ExecuteNonQuery();
    }

    private static void UnregisterRunningClient(SqliteConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RunningClient WHERE ProcessId = $processId";
            command.Parameters.AddWithValue("$processId", Environment.ProcessId);
            command.ExecuteNonQuery();
        }
        catch (SqliteException e)
        {
            Console.Error.WriteLine($"Failed to unregister running client:{e.Message}");
        }
    }

    private static string MainModulePath()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.MainModule?.FileName ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static SqliteConnection Open(string path, bool readOnly)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());

        connection.Open();
        return connection;
    }

    private static int PoolSize()
    {
        var configured = Environment.GetEnvironmentVariable("SPACEWAY_CONTENT_POOL_SIZE");

        if (!string.IsNullOrEmpty(configured) && int.TryParse(configured, out var size) && size > 0)
            return size;

        return Math.Max(2, Environment.ProcessorCount);
    }

    private readonly record struct FileEntry(long ContentId, int Size, ContentCompression Compression);

    private sealed record Reader(
        SqliteConnection Connection,
        SqliteTransaction Transaction,
        bool OwnsConnection);

    /// <summary>Mirrors values from the content database; must not change.</summary>
    private enum ContentCompression
    {
        None = 0,
        Deflate = 1,
        ZStd = 2,
    }
}
