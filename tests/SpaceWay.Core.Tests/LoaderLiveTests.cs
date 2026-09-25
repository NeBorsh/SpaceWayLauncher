using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;
using SpaceWay.Loader;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Loading the real Robust engine directly from the archive.
/// </summary>
[TestFixture]
[Explicit("Требует сети и качает настоящую сборку движка")]
[Category("Live")]
public sealed class LoaderLiveTests
{
    private string _tempRoot = null!;
    private string _enginePath = null!;
    private string _signature = null!;

    [OneTimeSetUp]
    public async Task DownloadEngine()
    {
        _tempRoot = TestPaths.CreateTempRoot("loader-live");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var db = LauncherDatabase.CreateInMemory();

        var store = new EngineStore(db);
        var api = new RobustBuildsApi(http);
        var manager = new EngineManager(
            store, api, http,
            engineDir: Path.Combine(_tempRoot, "engines"),
            moduleDir: Path.Combine(_tempRoot, "modules"));

        var builds = await api.GetBuilds();
        var newest = builds.Versions.Keys
            .Select(key => (Key: key, Parsed: Version.TryParse(key, out var v) ? v : null))
            .Where(v => v.Parsed != null)
            .MaxBy(v => v.Parsed)
            .Key;

        var installed = await manager.EnsureEngine(newest);

        _enginePath = manager.GetEnginePath(installed.Version);
        _signature = manager.GetEngineSignature(installed.Version);

        TestContext.Out.WriteLine($"Engine {installed.Version}: {_enginePath}");
    }

    [OneTimeTearDown]
    public void Cleanup() => TestPaths.DeleteQuietly(_tempRoot);

    [Test]
    public void EngineArchiveContainsClientAssembly()
    {
        using var engine = new EngineAssemblies(_enginePath);

        Assert.That(engine.Files.AllFiles, Does.Contain($"{EngineAssemblies.RobustAssemblyName}.dll"));
    }

    [Test]
    public void ClientAssemblyLoadsFromArchive()
    {
        using var engine = new EngineAssemblies(_enginePath);

        Assert.That(engine.TryLoad(EngineAssemblies.RobustAssemblyName, out var client), Is.True);
        Assert.That(client!.GetName().Name, Is.EqualTo(EngineAssemblies.RobustAssemblyName));
    }

    [Test]
    public void EntryPointIsFoundAndCreated()
    {
        using var engine = new EngineAssemblies(_enginePath);

        Assert.That(engine.TryLoad(EngineAssemblies.RobustAssemblyName, out var client), Is.True);
        Assert.That(engine.TryGetEntryPoint(client!, out var entryPoint), Is.True);
        Assert.That(entryPoint, Is.Not.Null);

        TestContext.Out.WriteLine($"Engine entry point: {entryPoint!.GetType().FullName}");
    }

    [Test]
    public void SignatureFromManifestMatchesDownloadedFile()
    {
        Assert.That(EngineSignature.Robust.Verify(_enginePath, _signature), Is.True);
    }
}
