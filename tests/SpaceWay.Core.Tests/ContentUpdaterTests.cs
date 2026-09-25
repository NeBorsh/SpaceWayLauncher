using System.Text;
using Dapper;
using NSec.Cryptography;
using NUnit.Framework;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ContentUpdaterTests
{
    private ContentDatabase _content = null!;
    private LauncherDatabase _launcher = null!;
    private FakeContentServer _server = null!;
    private HttpClient _http = null!;
    private Key _engineKey = null!;
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _content = ContentDatabase.CreateInMemory();
        _launcher = LauncherDatabase.CreateInMemory();
        _tempRoot = TestPaths.CreateTempRoot("content");

        _engineKey = Key.Create(SignatureAlgorithm.Ed25519);

        _server = new FakeContentServer { EnginePayload = "движок"u8.ToArray() };
        _server.AddFile("Content.Client.dll", "клиентские сборки");
        _server.AddFile("Resources/maps/station.yml", "карта станции");
        _server.AddFile("Resources/prototypes/atmos.yml", "прототипы атмоса");

        _http = new HttpClient(_server);
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _engineKey.Dispose();
        _content.KeepAlive?.Dispose();
        _launcher.Dispose();

        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public async Task DownloadsContentAndEngine()
    {
        var result = await CreateUpdater().Prepare(_server.BuildInfoWithManifest());

        using var con = _content.Open();

        Assert.Multiple(() =>
        {
            Assert.That(result.VersionId, Is.GreaterThan(0));
            Assert.That(result.Modules, Is.EquivalentTo(new[] { ("Robust", "1.0.0") }));
            Assert.That(FileCount(result.VersionId), Is.EqualTo(3));
            Assert.That(_server.FilesSent, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task StoredFilesReadBackUnchanged()
    {
        var result = await CreateUpdater().Prepare(_server.BuildInfoWithManifest());

        Assert.That(ReadFile(result.VersionId, "Resources/maps/station.yml"), Is.EqualTo("карта станции"));
    }

    [Test]
    public async Task SecondConnectionDownloadsNothing()
    {
        var updater = CreateUpdater();
        await updater.Prepare(_server.BuildInfoWithManifest());

        var before = _server.FilesSent;
        await updater.Prepare(_server.BuildInfoWithManifest());

        Assert.Multiple(() =>
        {
            Assert.That(_server.FilesSent, Is.EqualTo(before), "files were downloaded again");
            Assert.That(_server.DownloadRequests, Is.EqualTo(1), "files were requested a second time");
        });
    }

    [Test]
    public async Task UpdatedServerDownloadsOnlyChangedFiles()
    {
        var updater = CreateUpdater();
        await updater.Prepare(_server.BuildInfoWithManifest());

        var updated = new FakeContentServer { EnginePayload = _server.EnginePayload };
        updated.AddFile("Content.Client.dll", "клиентские сборки");
        updated.AddFile("Resources/maps/station.yml", "карта станции, версия два");
        updated.AddFile("Resources/prototypes/atmos.yml", "прототипы атмоса");

        using var http = new HttpClient(updated);
        await CreateUpdater(http, updated).Prepare(updated.BuildInfoWithManifest());

        Assert.That(updated.FilesSent, Is.EqualTo(1), "the incremental download fetched more than needed");
    }

    [Test]
    public async Task PreCompressedStreamIsAccepted()
    {
        _server.PreCompress = true;

        var result = await CreateUpdater().Prepare(_server.BuildInfoWithManifest());

        Assert.That(ReadFile(result.VersionId, "Content.Client.dll"), Is.EqualTo("клиентские сборки"));
    }

    [Test]
    public void TamperedManifestIsRejected()
    {
        var buildInfo = _server.BuildInfoWithManifest() with
        {
            ManifestHash = new string('a', 64),
        };

        Assert.ThrowsAsync<ContentUpdateException>(() => CreateUpdater().Prepare(buildInfo));
    }

    [Test]
    public void UnsupportedProtocolIsRefusedBeforeDownloading()
    {
        _server.Protocols = (2, 3);

        Assert.ThrowsAsync<ContentUpdateException>(
            () => CreateUpdater().Prepare(_server.BuildInfoWithManifest()));

        Assert.That(_server.DownloadRequests, Is.Zero, "downloading started before the protocol was negotiated");
    }

    [Test]
    public async Task InterruptedDownloadKeepsWhatItGot()
    {
        _server.BreakAfterFiles = 1;

        Assert.ThrowsAsync<EndOfStreamException>(
            () => CreateUpdater().Prepare(_server.BuildInfoWithManifest()));

        using (var con = _content.Open())
        {
            Assert.Multiple(() =>
            {
                Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.Zero,
                    "the incomplete version remained in the database");
                Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM Content"), Is.EqualTo(1),
                    "the downloaded file was not kept for the next attempt");
                Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM InterruptedDownloadContent"),
                    Is.EqualTo(1));
            });
        }

        _server.BreakAfterFiles = 0;
        var before = _server.FilesSent;

        var result = await CreateUpdater().Prepare(_server.BuildInfoWithManifest());

        Assert.Multiple(() =>
        {
            Assert.That(_server.FilesSent - before, Is.EqualTo(2), "downloaded more than the missing files");
            Assert.That(FileCount(result.VersionId), Is.EqualTo(3));
        });
    }

    [Test]
    public void CorruptedFileIsRejected()
    {
        _server.CorruptFiles = true;

        Assert.ThrowsAsync<ContentUpdateException>(
            () => CreateUpdater().Prepare(_server.BuildInfoWithManifest()));

        using var con = _content.Open();
        Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.Zero,
            "the version with tampered files remained in the database");
    }

    [Test]
    public async Task ZipDeliveryWorksForServersWithoutManifest()
    {
        var result = await CreateUpdater().Prepare(_server.BuildInfoWithZip());

        Assert.Multiple(() =>
        {
            Assert.That(FileCount(result.VersionId), Is.EqualTo(3));
            Assert.That(ReadFile(result.VersionId, "Resources/prototypes/atmos.yml"),
                Is.EqualTo("прототипы атмоса"));
        });
    }

    [Test]
    public async Task ZipAndManifestProduceSameVersionHash()
    {
        var updater = CreateUpdater();
        var fromZip = await updater.Prepare(_server.BuildInfoWithZip());

        using var con = _content.Open();
        var storedHash = con.ExecuteScalar<byte[]>(
            "SELECT Hash FROM ContentVersion WHERE Id = @id", new { id = fromZip.VersionId });

        Assert.That(storedHash, Is.EqualTo(_server.ManifestHash()));
    }

    [Test]
    public void ModulesFromResourceManifestAreInstalled()
    {
        _server.AddFile("manifest.yml", "modules:\n- Robust.Client.WebView\n");

        var modules = new EngineModuleManifest(new Dictionary<string, EngineModuleData>
        {
            ["Robust.Client.WebView"] = new(new Dictionary<string, EngineModuleVersionData>
            {
                ["0.5.0"] = new(new Dictionary<string, EngineBuildInfo>
                {
                    [RidSelector.Current] = new(FakeContentServer.EngineUrl, "00", "00"),
                }),
            }),
        });

        Assert.ThrowsAsync<EngineUpdateException>(
            () => CreateUpdater(modules: modules).Prepare(_server.BuildInfoWithManifest()));

        using var con = _content.Open();
        var dependencies = con.Query<string>("SELECT ModuleName FROM ContentEngineDependency").ToList();

        Assert.That(dependencies, Does.Contain("Robust.Client.WebView"));
    }

    [Test]
    public async Task OldVersionsAreCulled()
    {
        var updater = CreateUpdater(cull: new ContentCullSettings(MaxVersions: 1, MaxVersionsPerFork: 1));

        await updater.Prepare(_server.BuildInfoWithManifest());

        await updater.Prepare(_server.BuildInfoWithManifest() with { ForkId = "другой" });

        using var con = _content.Open();
        Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.EqualTo(1));
    }

    [Test]
    public async Task ContentUsedByRunningGameIsNotCulled()
    {
        var updater = CreateUpdater(cull: new ContentCullSettings(MaxVersions: 1, MaxVersionsPerFork: 1));
        var first = await updater.Prepare(_server.BuildInfoWithManifest());

        using (var con = _content.Open())
        {
            var self = System.Diagnostics.Process.GetCurrentProcess();
            ContentStore.RegisterRunningClient(
                con, self.Id, self.MainModule!.FileName, first.VersionId);
        }

        await updater.Prepare(_server.BuildInfoWithManifest() with { ForkId = "другой" });

        using var check = _content.Open();
        Assert.That(
            check.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM ContentVersion WHERE Id = @id", new { id = first.VersionId }),
            Is.EqualTo(1),
            "content was deleted while the game was running");
    }

    [Test]
    public async Task ClearContentLeavesNothingBehind()
    {
        var updater = CreateUpdater();
        await updater.Prepare(_server.BuildInfoWithManifest());

        updater.ClearContent();

        using var con = _content.Open();

        Assert.Multiple(() =>
        {
            Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.Zero);
            Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM Content"), Is.Zero);
            Assert.That(Directory.GetFiles(Path.Combine(_tempRoot, "engines")), Is.Not.Empty);
        });
    }

    [Test]
    public async Task ClearEnginesRemovesBuilds()
    {
        var updater = CreateUpdater();
        await updater.Prepare(_server.BuildInfoWithManifest());

        updater.ClearEngines();

        using var con = _content.Open();

        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(Path.Combine(_tempRoot, "engines")), Is.Empty);
            Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ClearRefusesWhileGameRuns()
    {
        var updater = CreateUpdater();
        var result = await updater.Prepare(_server.BuildInfoWithManifest());

        using (var con = _content.Open())
        {
            ContentStore.RegisterRunningClient(
                con,
                Environment.ProcessId,
                Environment.ProcessPath!,
                result.VersionId);
        }

        var content = Assert.Throws<ContentUpdateException>(() => updater.ClearContent());
        var engines = Assert.Throws<ContentUpdateException>(() => updater.ClearEngines());

        Assert.Multiple(() =>
        {
            Assert.That(content!.Key, Is.EqualTo("error-clear-while-playing"));
            Assert.That(engines!.Key, Is.EqualTo("error-clear-while-playing"));
        });

        using var check = _content.Open();
        Assert.That(check.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.EqualTo(1));
    }

    private ContentUpdater CreateUpdater(
        HttpClient? http = null,
        FakeContentServer? server = null,
        EngineModuleManifest? modules = null,
        ContentCullSettings? cull = null)
    {
        http ??= _http;
        server ??= _server;

        var engines = new EngineManager(
            new EngineStore(_launcher),
            new StubManifests(server.EngineManifest(_engineKey), modules ?? new EngineModuleManifest([])),
            http,
            new EngineSignature(_engineKey.PublicKey),
            Path.Combine(_tempRoot, "engines"),
            Path.Combine(_tempRoot, "modules"));

        return new ContentUpdater(_content, engines, http, cull);
    }

    [Test]
    public async Task BundleWithoutBaseBuildGoesIntoDatabase()
    {
        var path = ContentBundleTests.Write(_tempRoot, "bundle.zip",
            """{ "engine_version": "1.0.0" }""",
            ("Content.Client.dll", "сборки бандла"),
            ("Resources/maps/test.yml", "карта бандла"));

        using var bundle = ContentBundle.Open(path);
        var result = await CreateUpdater().PrepareBundle(bundle);

        Assert.Multiple(() =>
        {
            Assert.That(ReadFile(result.VersionId, "Resources/maps/test.yml"), Is.EqualTo("карта бандла"));
            Assert.That(result.Modules, Is.EquivalentTo(new[] { ("Robust", "1.0.0") }));

            Assert.That(_server.FilesSent, Is.Zero);
        });
    }

    [Test]
    public async Task BundleLiesOverItsBaseBuild()
    {
        var baseBuild = _server.BuildInfoWithManifest();
        var metadata = $$"""
            {
              "engine_version": "1.0.0",
              "base_build": {
                "fork_id": "{{baseBuild.ForkId}}",
                "version": "{{baseBuild.Version}}",
                "manifest_url": "{{baseBuild.ManifestUrl}}",
                "manifest_download_url": "{{baseBuild.ManifestDownloadUrl}}",
                "manifest_hash": "{{baseBuild.ManifestHash}}"
              }
            }
            """;

        var path = ContentBundleTests.Write(_tempRoot, "bundle.zip", metadata,
            ("manifest.yml", "modules: []"),
            ("Resources/maps/station.yml", "карта из бандла"));

        using var bundle = ContentBundle.Open(path);
        var result = await CreateUpdater().PrepareBundle(bundle);

        Assert.Multiple(() =>
        {
            Assert.That(ReadFile(result.VersionId, "Resources/maps/station.yml"), Is.EqualTo("карта из бандла"));
            Assert.That(ReadFile(result.VersionId, "Resources/prototypes/atmos.yml"), Is.EqualTo("прототипы атмоса"));
        });
    }

    [Test]
    public async Task SameBundleTwiceIsStoredOnce()
    {
        var path = ContentBundleTests.Write(_tempRoot, "bundle.zip",
            """{ "engine_version": "1.0.0" }""",
            ("Resources/maps/test.yml", "карта бандла"));

        var updater = CreateUpdater();

        long first, second;
        using (var bundle = ContentBundle.Open(path))
            first = (await updater.PrepareBundle(bundle)).VersionId;
        using (var bundle = ContentBundle.Open(path))
            second = (await updater.PrepareBundle(bundle)).VersionId;

        using var con = _content.Open();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(con.ExecuteScalar<long>("SELECT COUNT(*) FROM ContentVersion"), Is.EqualTo(1));
        });
    }

    private long FileCount(long versionId)
    {
        using var con = _content.Open();
        return con.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM ContentManifest WHERE VersionId = @versionId", new { versionId });
    }

    private string? ReadFile(long versionId, string path)
    {
        using var con = _content.Open();
        using var file = ContentStore.OpenFile(con, versionId, path);
        if (file == null)
            return null;

        using var reader = new StreamReader(file, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class StubManifests(EngineBuildManifest builds, EngineModuleManifest modules)
        : IEngineManifestSource
    {
        public Task<EngineBuildManifest> GetBuilds(CancellationToken cancel = default) =>
            Task.FromResult(builds);

        public Task<EngineModuleManifest> GetModules(CancellationToken cancel = default) =>
            Task.FromResult(modules);
    }
}
