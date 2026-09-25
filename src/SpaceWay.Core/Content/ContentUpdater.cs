using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SpaceWay.Core.Engine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpaceWay.Core.Content;

/// <summary>
/// Brings content and engine into a state ready to launch the game.
/// </summary>
public sealed class ContentUpdater(
    ContentDatabase database,
    EngineManager engines,
    HttpClient http,
    ContentCullSettings? cullSettings = null)
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly ManifestDownloader _manifest = new(http);
    private readonly ZipDownloader _zip = new(http);
    private readonly ContentCullSettings _cull = cullSettings ?? new ContentCullSettings();

    /// <summary>
    /// Deletes downloaded server content: all versions and their files.
    /// </summary>
    public void ClearContent()
    {
        using var con = database.Open();
        EnsureNobodyPlaying(con);

        con.Execute("DELETE FROM InterruptedDownload");
        con.Execute("DELETE FROM ContentVersion");
        con.Execute("DELETE FROM Content");
        con.Execute("VACUUM");

        Log.Information("Server content deleted at the player's request");
    }

    /// <summary>
    /// Deletes downloaded engine builds and modules.
    /// </summary>
    public void ClearEngines()
    {
        using (var con = database.Open())
        {
            EnsureNobodyPlaying(con);
        }

        engines.ClearAll();

        Log.Information("Engine builds deleted at the player's request");
    }

    /// <summary>
    /// Refuses to touch downloaded data while the game is running.
    /// </summary>
    private static void EnsureNobodyPlaying(SqliteConnection con)
    {
        if (ContentStore.GetRunningClientVersions(con).Count > 0)
            throw new ContentUpdateException("error-clear-while-playing");
    }

    /// <summary>
    /// Prepares everything needed to connect to a server.
    /// </summary>
    public async Task<ContentLaunchInfo> Prepare(
        ServerBuildInformation buildInfo,
        IProgress<ContentProgress>? progress = null,
        CancellationToken cancel = default)
    {
        using var con = database.Open();

        var moduleManifest = new Lazy<Task<EngineModuleManifest>>(
            () => engines.GetModuleManifest(cancel));

        progress?.Report(new ContentProgress(ContentStage.CheckingVersion));

        var versionId = await EnsureContent(buildInfo, con, moduleManifest, progress, cancel);

        progress?.Report(new ContentProgress(ContentStage.Culling));
        await Task.Run(
            () => ContentStore.CullOldVersions(
                con, _cull.MaxVersions, _cull.MaxVersionsPerFork, _cull.KeepInterrupted),
            CancellationToken.None);

        return await EnsureEngines(con, versionId, moduleManifest, progress, cancel);
    }

    /// <summary>
    /// Prepares everything needed to run a bundle entirely from the content database.
    /// </summary>
    public async Task<ContentLaunchInfo> PrepareBundle(
        ContentBundle bundle,
        IProgress<ContentProgress>? progress = null,
        CancellationToken cancel = default)
    {
        using var con = database.Open();

        var moduleManifest = new Lazy<Task<EngineModuleManifest>>(
            () => engines.GetModuleManifest(cancel));

        progress?.Report(new ContentProgress(ContentStage.CheckingVersion));

        var key = await Task.Run(bundle.ComputeKey, cancel);
        var keyHex = Convert.ToHexString(key);

        var identity = new ServerBuildInformation
        {
            ForkId = ContentBundle.ForkId,
            Version = keyHex,
            EngineVersion = bundle.Metadata.EngineVersion,
            Hash = keyHex,
        };

        long versionId;

        if (ContentStore.FindExisting(con, identity) is { } existing)
        {
            Log.Information("Bundle already unpacked, version {VersionId}", existing.Id);
            ContentStore.TouchVersion(con, existing.Id);
            versionId = existing.Id;
        }
        else
        {
            long? baseVersion = bundle.Metadata.BaseBuild == null
                ? null
                : await EnsureContent(
                    bundle.Metadata.BaseBuildInformation(), con, moduleManifest, progress, cancel);

            versionId = await Task.Run(
                () => IngestBundle(bundle, con, identity, key, baseVersion, progress, cancel),
                cancel);

            await ResolveBundleDependencies(con, versionId, bundle.Metadata.EngineVersion, moduleManifest);
        }

        progress?.Report(new ContentProgress(ContentStage.Culling));
        await Task.Run(
            () => ContentStore.CullOldVersions(
                con, _cull.MaxVersions, _cull.MaxVersionsPerFork, _cull.KeepInterrupted),
            CancellationToken.None);

        return await EnsureEngines(con, versionId, moduleManifest, progress, cancel);
    }

    private static long IngestBundle(
        ContentBundle bundle,
        SqliteConnection con,
        ServerBuildInformation identity,
        byte[] key,
        long? baseVersion,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        using var transaction = con.BeginTransaction();

        var versionId = ContentStore.CreateVersion(con, identity);
        ContentStore.SetZipHash(con, versionId, key);

        if (baseVersion is { } source)
        {
            con.Execute(
                """
                INSERT INTO ContentManifest (VersionId, Path, ContentId)
                SELECT @versionId, Path, ContentId FROM ContentManifest WHERE VersionId = @source
                """,
                new { versionId, source });
        }

        progress?.Report(new ContentProgress(ContentStage.StoringFiles));
        ZipDownloader.Ingest(con, versionId, bundle.Archive, progress, cancel, overwrite: true);

        ContentStore.SetManifestHash(con, versionId, ContentStore.ComputeManifestHash(con, versionId));

        progress?.Report(new ContentProgress(ContentStage.Committing));
        transaction.Commit();

        Log.Information("Bundle unpacked as version {VersionId}", versionId);
        return versionId;
    }

    /// <summary>
    /// Bundle engine dependencies, in a separate transaction.
    /// </summary>
    private static async Task ResolveBundleDependencies(
        SqliteConnection con,
        long versionId,
        string engineVersion,
        Lazy<Task<EngineModuleManifest>> moduleManifest)
    {
        try
        {
            await ResolveDependencies(con, versionId, engineVersion, moduleManifest);
        }
        catch
        {
            ContentStore.DeleteVersion(con, versionId);
            throw;
        }
    }

    /// <summary>
    /// In a single transaction: finds a ready content version or downloads a new one.
    /// </summary>
    private async Task<long> EnsureContent(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        using var transaction = con.BeginTransaction();

        var downloadedBlobs = new List<long>();
        long? createdVersion = null;

        try
        {
            long versionId;

            if (ContentStore.FindExisting(con, buildInfo) is { } existing)
            {
                versionId = await ReuseExisting(buildInfo, con, existing, moduleManifest);
            }
            else
            {
                createdVersion = versionId = ContentStore.CreateVersion(con, buildInfo);
                await DownloadNew(buildInfo, con, versionId, downloadedBlobs, moduleManifest, progress, cancel);
            }

            progress?.Report(new ContentProgress(ContentStage.Committing));
            transaction.Commit();

            return versionId;
        }
        catch (Exception e) when (e is not SqliteException)
        {
            if (downloadedBlobs.Count == 0)
                throw;

            Log.Warning(
                "Download interrupted, keeping {Count} downloaded files for later",
                downloadedBlobs.Count);

            if (createdVersion is { } version)
                ContentStore.DeleteVersion(con, version);

            ContentStore.SaveInterruptedDownload(con, downloadedBlobs);
            transaction.Commit();

            throw;
        }
    }

    /// <summary>
    /// Adapts already downloaded content to what the server reported.
    /// </summary>
    private static async Task<long> ReuseExisting(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        ContentVersionRow existing,
        Lazy<Task<EngineModuleManifest>> moduleManifest)
    {
        var dependencies = ContentStore.GetEngineDependencies(con, existing.Id);
        var currentEngine = dependencies
            .FirstOrDefault(d => d.Name == ContentStore.RobustModuleName).Version;

        var sameFork = buildInfo.ForkId == existing.ForkId && buildInfo.Version == existing.ForkVersion;
        var sameEngine = buildInfo.EngineVersion == currentEngine;

        if (sameFork && sameEngine)
        {
            ContentStore.TouchVersion(con, existing.Id);
            return existing.Id;
        }

        Log.Debug("Build info differs from the stored one, creating a separate version");

        var versionId = ContentStore.DuplicateVersion(con, existing, buildInfo);

        if (sameEngine)
        {
            ContentStore.CopyEngineDependencies(con, existing.Id, versionId);
            return versionId;
        }

        ContentStore.AddEngineDependency(
            con, versionId, ContentStore.RobustModuleName, buildInfo.EngineVersion);

        var modules = dependencies.Where(d => d.Name != ContentStore.RobustModuleName).ToArray();
        if (modules.Length > 0)
        {
            var manifest = await moduleManifest.Value;
            foreach (var (name, _) in modules)
            {
                ContentStore.AddEngineDependency(
                    con, versionId, name, manifest.ResolveVersion(name, buildInfo.EngineVersion));
            }
        }

        return versionId;
    }

    private async Task DownloadNew(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        List<long> downloadedBlobs,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        byte[] manifestHash;

        if (buildInfo.SupportsManifest)
        {
            manifestHash = await _manifest.Download(
                buildInfo, con, versionId, downloadedBlobs, progress, cancel);
        }
        else if (!string.IsNullOrEmpty(buildInfo.DownloadUrl))
        {
            manifestHash = await _zip.Download(buildInfo, con, versionId, progress, cancel);
        }
        else
        {
            throw new ContentUpdateException("error-no-content-source");
        }

        ContentStore.SetManifestHash(con, versionId, manifestHash);

        await ResolveDependencies(con, versionId, buildInfo.EngineVersion, moduleManifest);
    }

    /// <summary>Records the version's engine requirements.</summary>
    private static async Task ResolveDependencies(
        SqliteConnection con,
        long versionId,
        string engineVersion,
        Lazy<Task<EngineModuleManifest>> moduleManifest)
    {
        ContentStore.AddEngineDependency(con, versionId, ContentStore.RobustModuleName, engineVersion);

        var modules = ReadRequiredModules(con, versionId);
        if (modules.Length == 0)
            return;

        var manifest = await moduleManifest.Value;

        foreach (var module in modules)
        {
            ContentStore.AddEngineDependency(
                con, versionId, module, manifest.ResolveVersion(module, engineVersion));
        }
    }

    /// <summary>
    /// Reads the required engine modules from the build manifest.
    /// </summary>
    private static string[] ReadRequiredModules(SqliteConnection con, long versionId)
    {
        using var file = ContentStore.OpenFile(con, versionId, "manifest.yml");
        if (file == null)
            return [];

        try
        {
            using var reader = new StreamReader(file);
            return YamlDeserializer.Deserialize<ResourceManifest?>(reader)?.Modules ?? [];
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to parse the build's manifest.yml");
            return [];
        }
    }

    /// <summary>Delivers the engine and modules required by a content version.</summary>
    private async Task<ContentLaunchInfo> EnsureEngines(
        SqliteConnection con,
        long versionId,
        Lazy<Task<EngineModuleManifest>> moduleManifest,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        var modules = ContentStore.GetEngineDependencies(con, versionId);

        for (var i = 0; i < modules.Length; i++)
        {
            var (name, version) = modules[i];

            if (name == ContentStore.RobustModuleName)
            {
                progress?.Report(new ContentProgress(ContentStage.DownloadingEngine));

                var installed = await engines.EnsureEngine(
                    version,
                    (done, total) => progress?.Report(new ContentProgress(
                        ContentStage.DownloadingEngine, done, total, InBytes: true)),
                    cancel);

                modules[i] = (name, installed.Version);
            }
            else
            {
                progress?.Report(new ContentProgress(ContentStage.DownloadingModules));

                await engines.EnsureModule(
                    name,
                    version,
                    await moduleManifest.Value,
                    (done, total) => progress?.Report(new ContentProgress(
                        ContentStage.DownloadingModules, done, total, InBytes: true)),
                    cancel);
            }
        }

        progress?.Report(new ContentProgress(ContentStage.Culling));
        await engines.CullUnused(ContentStore.GetAllEngineDependencies(con), cancel);

        progress?.Report(new ContentProgress(ContentStage.Done));
        Log.Information("Content and engine are ready");

        return new ContentLaunchInfo(versionId, modules);
    }

    private sealed class ResourceManifest
    {
        public string[]? Modules { get; set; }
    }
}

/// <summary>
/// How much downloaded content to keep on disk.
/// </summary>
/// <param name="MaxVersions">Total versions.</param>
/// <param name="MaxVersionsPerFork">
/// Versions per fork. A separate limit keeps a player who visits many servers
/// of the same fork from evicting all other content.
/// </param>
/// <param name="KeepInterrupted">
/// How long to keep files of interrupted downloads so they can be resumed.
/// </param>
public sealed record ContentCullSettings(
    int MaxVersions = 5,
    int MaxVersionsPerFork = 3,
    TimeSpan KeepInterrupted = default)
{
    public TimeSpan KeepInterrupted { get; init; } =
        KeepInterrupted == default ? TimeSpan.FromHours(24) : KeepInterrupted;
}
