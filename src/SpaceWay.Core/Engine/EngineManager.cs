using Serilog;
using SpaceWay.Core.Data;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Engine;

/// <summary>Result of checking the engine before launch.</summary>
/// <param name="Version">
/// Version actually installed. May differ from the requested one, since
/// the manifest can redirect revoked versions to fixed ones.
/// </param>
/// <param name="Changed">Whether anything had to be downloaded.</param>
public readonly record struct EngineInstallation(string Version, bool Changed);

/// <summary>
/// Downloads and tracks engine builds.
/// </summary>
public sealed class EngineManager(
    EngineStore store,
    IEngineManifestSource manifests,
    HttpClient http,
    EngineSignature? signature = null,
    string? engineDir = null,
    string? moduleDir = null)
{
    private readonly EngineSignature _signature = signature ?? EngineSignature.Robust;
    private readonly string _engineDir = engineDir ?? LauncherPaths.DirEngineInstallations;
    private readonly string _moduleDir = moduleDir ?? LauncherPaths.DirModuleInstallations;

    /// <summary>Path to the archive of an installed engine version.</summary>
    public string GetEnginePath(string version)
    {
        if (store.FindEngine(version) == null)
            throw new ArgumentException($"Engine version {version} is not installed", nameof(version));

        return EnginePath(version);
    }

    /// <summary>Signature of an installed version, passed to the loader.</summary>
    public string GetEngineSignature(string version) =>
        store.FindEngine(version)?.Signature
        ?? throw new ArgumentException($"Engine version {version} is not installed", nameof(version));

    public string GetModulePath(string moduleName, string moduleVersion) =>
        Path.Combine(_moduleDir, moduleName, moduleVersion);

    public Task<EngineModuleManifest> GetModuleManifest(CancellationToken cancel = default) =>
        manifests.GetModules(cancel);

    /// <summary>
    /// Ensures an engine version is downloaded, downloading it if needed.
    /// </summary>
    public async Task<EngineInstallation> EnsureEngine(
        string version,
        DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
        var manifest = await manifests.GetBuilds(cancel);

        var found = manifest.Find(version);
        if (found == null)
        {
            if (manifests is RobustBuildsApi api)
            {
                api.InvalidateBuilds();
                found = (await manifests.GetBuilds(cancel)).Find(version);
            }

            if (found == null)
                throw new EngineUpdateException("error-engine-version-unknown", ("version", version));
        }

        if (found.Info.Insecure)
            throw new EngineUpdateException("error-engine-insecure", ("version", found.Version));

        if (found.Version != version)
            Log.Debug("Engine version {Requested} redirected to {Found}", version, found.Version);

        if (store.FindEngine(found.Version) != null && File.Exists(EnginePath(found.Version)))
            return new EngineInstallation(found.Version, false);

        var rid = RidSelector.FindBest(found.Info.Platforms.Keys)
                  ?? throw new NoEngineForPlatformException("error-engine-no-platform",
                      ("version", found.Version), ("platform", RidSelector.Current));

        var build = found.Info.Platforms[rid];

        Log.Information("Downloading engine {Version} ({Rid})", found.Version, rid);

        FileHelpers.EnsureDirectoryExists(_engineDir);

        await DownloadVerified(
            build, EnginePath(found.Version),
            "error-engine-hash-mismatch", "error-engine-signature-mismatch",
            [("version", found.Version)], progress, cancel);

        store.AddEngine(new InstalledEngine(found.Version, build.Signature));
        return new EngineInstallation(found.Version, true);
    }

    /// <summary>
    /// Ensures the required module version is extracted to disk.
    /// </summary>
    /// <returns>Whether anything had to be downloaded.</returns>
    public async Task<bool> EnsureModule(
        string moduleName,
        string engineVersion,
        EngineModuleManifest manifest,
        DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
        var moduleVersion = manifest.ResolveVersion(moduleName, engineVersion);
        var versionData = manifest.Modules[moduleName].Versions[moduleVersion];

        if (versionData.Insecure)
            throw new EngineUpdateException("error-module-insecure",
                ("module", moduleName), ("version", moduleVersion));

        var modulePath = GetModulePath(moduleName, moduleVersion);

        if (store.HasModule(moduleName, moduleVersion) && Directory.Exists(modulePath))
            return false;

        var rid = RidSelector.FindBest(versionData.Platforms.Keys)
                  ?? throw new NoEngineForPlatformException("error-module-no-platform",
                      ("module", moduleName), ("platform", RidSelector.Current));

        var build = versionData.Platforms[rid];

        Log.Information("Downloading module {Module} {Version} ({Rid})", moduleName, moduleVersion, rid);

        using var temp = new FileHelpers.TempPath();
        await DownloadVerified(
            build, temp.Path,
            "error-module-hash-mismatch", "error-module-signature-mismatch",
            [("module", moduleName)], progress, cancel);

        await Task.Run(() =>
        {
            FileHelpers.EnsureDirectoryExists(modulePath);
            FileHelpers.ClearDirectory(modulePath);

            FileHelpers.MarkDirectoryCompressed(modulePath);

            using var archive = File.OpenRead(temp.Path);
            FileHelpers.ExtractZipToDirectory(modulePath, archive);

            MakeModuleExecutable(moduleName, modulePath);
        }, cancel);

        store.AddModule(new InstalledEngineModule(moduleName, moduleVersion));

        return true;
    }

    /// <summary>
    /// Deletes builds no longer needed.
    /// </summary>
    /// <param name="inUse">
    /// What to keep: module/version pairs, with the engine listed as Robust.
    /// The list comes from installed content, since the content knows which
    /// version it needs, not the engine.
    /// </param>
    public async Task CullUnused(
        IReadOnlyCollection<(string Name, string Version)> inUse,
        CancellationToken cancel = default)
    {
        var keep = new HashSet<(string, string)>();
        EngineBuildManifest? builds = null;

        foreach (var (name, version) in inUse)
        {
            if (name != RobustModuleName)
            {
                keep.Add((name, version));
                continue;
            }

            builds ??= await manifests.GetBuilds(cancel);
            keep.Add((RobustModuleName, builds.Find(version)?.Version ?? version));
        }

        foreach (var engine in store.GetEngines())
        {
            if (keep.Contains((RobustModuleName, engine.Version)))
                continue;

            Log.Debug("Deleting unused engine {Version}", engine.Version);
            store.RemoveEngine(engine.Version);
            Delete(() => File.Delete(EnginePath(engine.Version)));
        }

        foreach (var module in store.GetModules())
        {
            if (keep.Contains((module.Name, module.Version)))
                continue;

            Log.Debug("Deleting unused module {Module} {Version}", module.Name, module.Version);
            store.RemoveModule(module);
            Delete(() => Directory.Delete(GetModulePath(module.Name, module.Version), recursive: true));
        }
    }

    /// <summary>Forgets and deletes all builds. The next launch downloads them again.</summary>
    public void ClearAll()
    {
        foreach (var engine in store.GetEngines())
            store.RemoveEngine(engine.Version);

        foreach (var module in store.GetModules())
            store.RemoveModule(module);

        FileHelpers.ClearDirectory(_engineDir);
        FileHelpers.ClearDirectory(_moduleDir);
    }

    /// <summary>Name the engine is listed under in content dependencies.</summary>
    public const string RobustModuleName = "Robust";

    private string EnginePath(string version) => Path.Combine(_engineDir, $"{version}.zip");

    /// <summary>
    /// Downloads a build and verifies it before it is used.
    /// </summary>
    private async Task DownloadVerified(
        EngineBuildInfo build,
        string path,
        string hashFailedKey,
        string signatureFailedKey,
        (string Name, object Value)[] args,
        DownloadProgressCallback? progress,
        CancellationToken cancel)
    {
        Log.Debug("Downloading build: {Url}", build.Url);

        try
        {
            await using (var stream = File.Create(path, 4096, FileOptions.Asynchronous))
            {
                await http.DownloadToStream(build.Url, stream, progress, cancel);
            }

            if (!await EngineSignature.VerifyHash(path, build.Sha256, cancel))
                throw new EngineUpdateException(hashFailedKey, args);

            if (!_signature.Verify(path, build.Signature))
                throw new EngineUpdateException(signatureFailedKey, args);
        }
        catch
        {
            Delete(() => File.Delete(path));
            throw;
        }
    }

    private static void MakeModuleExecutable(string moduleName, string modulePath)
    {
        if (!OperatingSystem.IsLinux())
            return;

        if (moduleName != "Robust.Client.WebView")
            return;

        var executable = Path.Combine(modulePath, "Robust.Client.WebView");
        if (File.Exists(executable))
            FileHelpers.MakeExecutable(executable);
    }

    /// <summary>
    /// Deletion that is allowed to fail.
    /// </summary>
    private static void Delete(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warning(e, "Failed to delete build files");
        }
    }
}
