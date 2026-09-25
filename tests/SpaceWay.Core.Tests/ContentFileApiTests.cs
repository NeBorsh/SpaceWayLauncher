using System.Text;
using Dapper;
using NSec.Cryptography;
using NUnit.Framework;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;
using SpaceWay.Loader;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Reading game files from the content database, as the loader does.
/// </summary>
[TestFixture]
public sealed class ContentFileApiTests
{
    /// <summary>Long and repetitive enough to be stored compressed.</summary>
    private static readonly string Compressible =
        string.Concat(Enumerable.Repeat("атмосферный прототип ", 500));

    private string _tempRoot = null!;
    private string _databasePath = null!;
    private LauncherDatabase _launcher = null!;
    private FakeContentServer _server = null!;
    private HttpClient _http = null!;
    private Key _engineKey = null!;
    private long _versionId;

    [SetUp]
    public async Task SetUp()
    {
        _tempRoot = TestPaths.CreateTempRoot("loader");
        Directory.CreateDirectory(_tempRoot);
        _databasePath = Path.Combine(_tempRoot, "content.db");

        _launcher = LauncherDatabase.CreateInMemory();
        _engineKey = Key.Create(SignatureAlgorithm.Ed25519);

        _server = new FakeContentServer { EnginePayload = "движок"u8.ToArray() };
        _server.AddFile("Content.Client.dll", "клиентские сборки");
        _server.AddFile("Resources/prototypes/atmos.yml", Compressible);
        _http = new HttpClient(_server);

        var content = new ContentDatabase(_databasePath);
        content.Initialize();

        var engines = new EngineManager(
            new EngineStore(_launcher),
            new StubManifests(_server.EngineManifest(_engineKey)),
            _http,
            new EngineSignature(_engineKey.PublicKey),
            Path.Combine(_tempRoot, "engines"),
            Path.Combine(_tempRoot, "modules"));

        var result = await new ContentUpdater(content, engines, _http)
            .Prepare(_server.BuildInfoWithManifest());

        _versionId = result.VersionId;
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _engineKey.Dispose();
        _launcher.Dispose();
        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public void ReadsStoredFileBack()
    {
        using var api = new ContentFileApi(_databasePath, _versionId);

        Assert.That(ReadAll(api, "Content.Client.dll"), Is.EqualTo("клиентские сборки"));
    }

    [Test]
    public void ReadsCompressedFileBack()
    {
        using var api = new ContentFileApi(_databasePath, _versionId);

        Assert.That(ReadAll(api, "Resources/prototypes/atmos.yml"), Is.EqualTo(Compressible));
    }

    [Test]
    public void StoredFileIsActuallyCompressed()
    {
        using var con = new ContentDatabase(_databasePath).Open();

        var compression = con.ExecuteScalar<long>(
            """
            SELECT c.Compression FROM ContentManifest m
            INNER JOIN Content c ON c.Id = m.ContentId
            WHERE m.VersionId = @versionId AND m.Path = 'Resources/prototypes/atmos.yml'
            """,
            new { versionId = _versionId });

        Assert.That(compression, Is.Not.Zero, "the file was stored uncompressed");
    }

    [Test]
    public void ListsAllFiles()
    {
        using var api = new ContentFileApi(_databasePath, _versionId);

        Assert.That(api.AllFiles, Is.EquivalentTo(new[]
        {
            "Content.Client.dll",
            "Resources/prototypes/atmos.yml",
        }));
    }

    [Test]
    public void MissingFileIsReportedNotThrown()
    {
        using var api = new ContentFileApi(_databasePath, _versionId);

        Assert.Multiple(() =>
        {
            Assert.That(api.TryOpen("Resources/нет-такого.yml", out var stream), Is.False);
            Assert.That(stream, Is.Null);
        });
    }

    [Test]
    public void ConcurrentReadsAllSucceed()
    {
        using var api = new ContentFileApi(_databasePath, _versionId);

        var results = new string?[64];
        Parallel.For(0, results.Length, i =>
        {
            results[i] = i % 2 == 0
                ? ReadAll(api, "Content.Client.dll")
                : ReadAll(api, "Resources/prototypes/atmos.yml");
        });

        Assert.Multiple(() =>
        {
            Assert.That(results.Where((_, i) => i % 2 == 0), Is.All.EqualTo("клиентские сборки"));
            Assert.That(results.Where((_, i) => i % 2 != 0), Is.All.EqualTo(Compressible));
        });
    }

    [Test]
    public void RunningGameIsVisibleToLauncher()
    {
        using (var api = new ContentFileApi(_databasePath, _versionId))
        {
            using var con = new ContentDatabase(_databasePath).Open();

            Assert.That(
                con.ExecuteScalar<long>(
                    "SELECT UsedVersion FROM RunningClient WHERE ProcessId = @pid",
                    new { pid = Environment.ProcessId }),
                Is.EqualTo(_versionId),
                "the launcher won't know the game is running and will delete its content");

            _ = api;
        }

        using var after = new ContentDatabase(_databasePath).Open();
        Assert.That(
            after.ExecuteScalar<long>("SELECT COUNT(*) FROM RunningClient"),
            Is.Zero,
            "the running-game marker remained after exit");
    }

    [Test]
    public void EmptyVersionIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => new ContentFileApi(_databasePath, 9999));
    }

    private static string? ReadAll(ContentFileApi api, string path)
    {
        if (!api.TryOpen(path, out var stream))
            return null;

        using (stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    private sealed class StubManifests(EngineBuildManifest builds) : IEngineManifestSource
    {
        public Task<EngineBuildManifest> GetBuilds(CancellationToken cancel = default) =>
            Task.FromResult(builds);

        public Task<EngineModuleManifest> GetModules(CancellationToken cancel = default) =>
            Task.FromResult(new EngineModuleManifest([]));
    }
}
