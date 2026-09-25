using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using NSec.Cryptography;
using Serilog;
using SpaceWay.Vendor.ZStd;

namespace SpaceWay.Core.Content;

/// <summary>
/// Content database queries.
/// </summary>
public static class ContentStore
{
    /// <summary>Name the engine is listed under among dependencies.</summary>
    public const string RobustModuleName = "Robust";

    /// <summary>
    /// Looks up an already downloaded content version.
    /// </summary>
    public static ContentVersionRow? FindExisting(SqliteConnection con, ServerBuildInformation buildInfo)
    {
        const string ordering = """
            ORDER BY (ForkVersion = @ForkVersion
                AND ForkId = @ForkId
                AND (SELECT ModuleVersion FROM ContentEngineDependency d
                     WHERE d.VersionId = v.Id AND ModuleName = 'Robust') = @EngineVersion) DESC
            """;

        var parameters = new
        {
            buildInfo.ForkId,
            ForkVersion = buildInfo.Version,
            buildInfo.EngineVersion,
            ManifestHash = TryFromHex(buildInfo.ManifestHash),
            ZipHash = TryFromHex(buildInfo.Hash),
        };

        ContentVersionRow? found;
        if (parameters.ManifestHash != null)
        {
            found = con.QueryFirstOrDefault<ContentVersionRow>(
                $"SELECT * FROM ContentVersion v WHERE Hash = @ManifestHash {ordering}", parameters);
        }
        else if (parameters.ZipHash != null)
        {
            found = con.QueryFirstOrDefault<ContentVersionRow>(
                $"SELECT * FROM ContentVersion v WHERE ZipHash = @ZipHash {ordering}", parameters);
        }
        else
        {
            found = con.QueryFirstOrDefault<ContentVersionRow>(
                $"SELECT * FROM ContentVersion v WHERE ForkId = @ForkId AND ForkVersion = @ForkVersion {ordering}",
                parameters);
        }

        if (found == null)
            Log.Debug("Content version not found");
        else
            Log.Debug("Found existing content version {VersionId}", found.Id);

        return found;
    }

    public static long CreateVersion(SqliteConnection con, ServerBuildInformation buildInfo) =>
        con.ExecuteScalar<long>(
            """
            INSERT INTO ContentVersion (Hash, ForkId, ForkVersion, LastUsed, ZipHash)
            VALUES (zeroblob(32), @ForkId, @ForkVersion, datetime('now'), NULL)
            RETURNING Id
            """,
            new { buildInfo.ForkId, ForkVersion = buildInfo.Version });

    public static void SetManifestHash(SqliteConnection con, long versionId, byte[] hash) => con.Execute(
        "UPDATE ContentVersion SET Hash = @hash WHERE Id = @versionId", new { hash, versionId });

    public static void SetZipHash(SqliteConnection con, long versionId, byte[] hash) => con.Execute(
        "UPDATE ContentVersion SET ZipHash = @hash WHERE Id = @versionId", new { hash, versionId });

    public static void TouchVersion(SqliteConnection con, long versionId) => con.Execute(
        "UPDATE ContentVersion SET LastUsed = datetime('now') WHERE Id = @versionId", new { versionId });

    public static void DeleteVersion(SqliteConnection con, long versionId) => con.Execute(
        "DELETE FROM ContentVersion WHERE Id = @versionId", new { versionId });

    /// <summary>
    /// Copies a version with the same files but different build info.
    /// </summary>
    public static long DuplicateVersion(SqliteConnection con, ContentVersionRow source, ServerBuildInformation buildInfo)
    {
        var versionId = con.ExecuteScalar<long>(
            """
            INSERT INTO ContentVersion (Hash, ForkId, ForkVersion, LastUsed, ZipHash)
            VALUES (@Hash, @ForkId, @ForkVersion, datetime('now'), @ZipHash)
            RETURNING Id
            """,
            new { source.Hash, buildInfo.ForkId, ForkVersion = buildInfo.Version, source.ZipHash });

        con.Execute(
            """
            INSERT INTO ContentManifest (VersionId, Path, ContentId)
            SELECT @versionId, Path, ContentId FROM ContentManifest WHERE VersionId = @sourceId
            """,
            new { versionId, sourceId = source.Id });

        return versionId;
    }

    public static void AddEngineDependency(SqliteConnection con, long versionId, string name, string version)
    {
        con.Execute(
            """
            INSERT INTO ContentEngineDependency (VersionId, ModuleName, ModuleVersion)
            VALUES (@versionId, @name, @version)
            ON CONFLICT (VersionId, ModuleName) DO UPDATE SET ModuleVersion = excluded.ModuleVersion
            """,
            new { versionId, name, version });

        Log.Debug("Version {VersionId} requires {Module} {ModuleVersion}", versionId, name, version);
    }

    public static (string Name, string Version)[] GetEngineDependencies(SqliteConnection con, long versionId) =>
        con.Query<(string, string)>(
                "SELECT ModuleName, ModuleVersion FROM ContentEngineDependency WHERE VersionId = @versionId",
                new { versionId })
            .ToArray();

    /// <summary>Engine dependencies of all versions, used for engine cleanup.</summary>
    public static (string Name, string Version)[] GetAllEngineDependencies(SqliteConnection con) =>
        con.Query<(string, string)>(
                "SELECT DISTINCT ModuleName, ModuleVersion FROM ContentEngineDependency")
            .ToArray();

    public static void CopyEngineDependencies(SqliteConnection con, long sourceId, long versionId) => con.Execute(
        """
        INSERT INTO ContentEngineDependency (VersionId, ModuleName, ModuleVersion)
        SELECT @versionId, ModuleName, ModuleVersion FROM ContentEngineDependency WHERE VersionId = @sourceId
        """,
        new { versionId, sourceId });

    /// <summary>
    /// Opens a file of a version for reading.
    /// </summary>
    /// <returns>null if the version has no such file.</returns>
    public static Stream? OpenFile(SqliteConnection con, long versionId, string path)
    {
        var row = con.QueryFirstOrDefault<(long Id, long Compression)>(
            """
            SELECT c.Id, c.Compression FROM ContentManifest m, Content c
            WHERE m.VersionId = @versionId AND m.Path = @path AND c.Id = m.ContentId
            """,
            new { versionId, path });

        if (row.Id == 0)
            return null;

        var blob = new SqliteBlob(con, "Content", "Data", row.Id, readOnly: true);

        return (ContentCompression)row.Compression switch
        {
            ContentCompression.None => blob,
            ContentCompression.Deflate => new DeflateStream(blob, CompressionMode.Decompress),
            ContentCompression.ZStd => new ZStdDecompressStream(blob),
            _ => throw new ContentUpdateException("error-unknown-compression", null,
                ("kind", row.Compression)),
        };
    }

    /// <summary>
    /// Recomputes a version's manifest hash from what is actually stored.
    /// </summary>
    public static byte[] ComputeManifestHash(SqliteConnection con, long versionId)
    {
        var entries = con.Query<(string Path, byte[] Hash)>(
            """
            SELECT m.Path, c.Hash FROM ContentManifest m
            INNER JOIN Content c ON c.Id = m.ContentId
            WHERE m.VersionId = @versionId
            ORDER BY m.Path
            """,
            new { versionId });

        IncrementalHash.Initialize(HashAlgorithm.Blake2b_256, out var state);
        IncrementalHash.Update(ref state, "Robust Content Manifest 1\n"u8);

        foreach (var (path, hash) in entries)
        {
            IncrementalHash.Update(ref state, Encoding.UTF8.GetBytes($"{Convert.ToHexString(hash)} {path}\n"));
        }

        return IncrementalHash.Finalize(ref state);
    }

    /// <summary>
    /// Versions in use by running clients.
    /// </summary>
    public static HashSet<long> GetRunningClientVersions(SqliteConnection con)
    {
        var running = new HashSet<long>();
        var dead = new List<long>();

        foreach (var (pid, mainModule, version) in con.Query<(long Pid, string MainModule, long Version)>(
                     "SELECT ProcessId, MainModule, UsedVersion FROM RunningClient"))
        {
            if (IsStillRunning((int)pid, mainModule))
                running.Add(version);
            else
                dead.Add(pid);
        }

        foreach (var pid in dead)
        {
            Log.Debug("Unregistering dead client {Pid}", pid);
            con.Execute("DELETE FROM RunningClient WHERE ProcessId = @pid", new { pid });
        }

        return running;
    }

    public static void RegisterRunningClient(SqliteConnection con, int processId, string mainModule, long versionId) =>
        con.Execute(
            """
            INSERT INTO RunningClient (ProcessId, MainModule, UsedVersion)
            VALUES (@processId, @mainModule, @versionId)
            ON CONFLICT (ProcessId) DO UPDATE SET
                MainModule = excluded.MainModule,
                UsedVersion = excluded.UsedVersion
            """,
            new { processId, mainModule, versionId });

    public static void UnregisterRunningClient(SqliteConnection con, int processId) =>
        con.Execute("DELETE FROM RunningClient WHERE ProcessId = @processId", new { processId });

    /// <summary>
    /// Records blobs of an interrupted download so cleanup keeps them.
    /// </summary>
    public static void SaveInterruptedDownload(SqliteConnection con, IReadOnlyCollection<long> contentIds)
    {
        if (contentIds.Count == 0)
            return;

        var downloadId = con.ExecuteScalar<long>(
            "INSERT INTO InterruptedDownload (Added) VALUES (datetime('now')) RETURNING Id");

        foreach (var contentId in contentIds)
        {
            con.Execute(
                """
                INSERT INTO InterruptedDownloadContent (InterruptedDownloadId, ContentId)
                VALUES (@downloadId, @contentId)
                ON CONFLICT (ContentId) DO NOTHING
                """,
                new { downloadId, contentId });
        }
    }

    /// <summary>
    /// Removes old content versions and orphaned blobs.
    /// </summary>
    public static void CullOldVersions(
        SqliteConnection con,
        int maxVersions,
        int maxForkVersions,
        TimeSpan keepInterrupted)
    {
        var removedAnything = false;

        var versions = con.Query<ContentVersionRow>(
            "SELECT * FROM ContentVersion ORDER BY LastUsed DESC").ToArray();

        var inUse = GetRunningClientVersions(con);

        var perFork = new Dictionary<string, int>();
        var kept = 0;

        foreach (var version in versions)
        {
            var fork = version.ForkId ?? string.Empty;
            var forkKept = perFork.GetValueOrDefault(fork);

            if (forkKept < maxForkVersions && kept < maxVersions)
            {
                perFork[fork] = forkKept + 1;
                kept++;
                continue;
            }

            if (inUse.Contains(version.Id))
            {
                Log.Debug(
                    "Keeping version {ForkId}/{ForkVersion}: currently in use",
                    version.ForkId, version.ForkVersion);
                continue;
            }

            Log.Debug("Deleting content version {ForkId}/{ForkVersion}", version.ForkId, version.ForkVersion);
            DeleteVersion(con, version.Id);
            removedAnything = true;
        }

        var staleDownloads = con.Execute(
            "DELETE FROM InterruptedDownload WHERE Added < @threshold",
            new { threshold = DateTime.UtcNow - keepInterrupted });

        if (staleDownloads > 0)
        {
            Log.Debug("Deleted {Count} stale interrupted downloads", staleDownloads);
            removedAnything = true;
        }

        if (!removedAnything)
            return;

        var orphans = con.Execute(
            """
            DELETE FROM Content
            WHERE Id NOT IN (SELECT ContentId FROM ContentManifest)
              AND Id NOT IN (SELECT ContentId FROM InterruptedDownloadContent)
            """);

        Log.Debug("Deleted {Count} orphaned blobs", orphans);
    }

    private static bool IsStillRunning(int pid, string mainModule)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.MainModule?.FileName == mainModule;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
        catch (Exception e)
        {
            Log.Debug(e, "Failed to check process {Pid}, assuming alive", pid);
            return true;
        }
    }

    private static byte[]? TryFromHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return null;

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            Log.Warning("Server reported a hash that is not a hex string");
            return null;
        }
    }
}
