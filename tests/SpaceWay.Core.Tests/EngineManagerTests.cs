using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using NSec.Cryptography;
using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class EngineManagerTests
{
    private const string EngineUrl = "https://builds.example/engine.zip";
    private const string ModuleUrl = "https://builds.example/module.zip";

    private LauncherDatabase _db = null!;
    private EngineStore _store = null!;
    private string _tempRoot = null!;
    private Key _key = null!;
    private StubHandler _handler = null!;
    private HttpClient _http = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        _store = new EngineStore(_db);
        _tempRoot = TestPaths.CreateTempRoot("test");

        _key = Key.Create(SignatureAlgorithm.Ed25519);

        _handler = new StubHandler();
        _http = new HttpClient(_handler);
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _key.Dispose();
        _db.Dispose();

        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public async Task DownloadsEngineAndRecordsIt()
    {
        var payload = "движок"u8.ToArray();
        _handler.Responses[EngineUrl] = payload;

        var result = await CreateManager(BuildsWith("1.0.0", payload)).EnsureEngine("1.0.0");

        Assert.Multiple(() =>
        {
            Assert.That(result.Version, Is.EqualTo("1.0.0"));
            Assert.That(result.Changed, Is.True);
            Assert.That(_store.FindEngine("1.0.0"), Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(_tempRoot, "engines", "1.0.0.zip")), Is.True);
        });
    }

    [Test]
    public async Task InstalledEngineIsNotDownloadedTwice()
    {
        var payload = "движок"u8.ToArray();
        _handler.Responses[EngineUrl] = payload;
        var manager = CreateManager(BuildsWith("1.0.0", payload));

        await manager.EnsureEngine("1.0.0");
        var second = await manager.EnsureEngine("1.0.0");

        Assert.Multiple(() =>
        {
            Assert.That(second.Changed, Is.False);
            Assert.That(_handler.RequestCount[EngineUrl], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task DeletedArchiveIsDownloadedAgain()
    {
        var payload = "движок"u8.ToArray();
        _handler.Responses[EngineUrl] = payload;
        var manager = CreateManager(BuildsWith("1.0.0", payload));

        await manager.EnsureEngine("1.0.0");
        File.Delete(Path.Combine(_tempRoot, "engines", "1.0.0.zip"));

        var second = await manager.EnsureEngine("1.0.0");

        Assert.That(second.Changed, Is.True);
    }

    [Test]
    public void TamperedBuildIsNotLeftOnDisk()
    {
        var real = "движок"u8.ToArray();
        var manifest = BuildsWith("1.0.0", real);

        _handler.Responses[EngineUrl] = "подмена"u8.ToArray();

        var manager = CreateManager(manifest);

        Assert.ThrowsAsync<EngineUpdateException>(() => manager.EnsureEngine("1.0.0"));
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(_tempRoot, "engines", "1.0.0.zip")), Is.False,
                "an unverified file remained under the real name");
            Assert.That(_store.FindEngine("1.0.0"), Is.Null);
        });
    }

    [Test]
    public void ForeignSignatureIsRejected()
    {
        var payload = "движок"u8.ToArray();
        _handler.Responses[EngineUrl] = payload;

        using var otherKey = Key.Create(SignatureAlgorithm.Ed25519);
        var manifest = new EngineBuildManifest(new Dictionary<string, EngineVersionInfo>
        {
            ["1.0.0"] = new(false, null, new Dictionary<string, EngineBuildInfo>
            {
                [RidSelector.Current] = new(EngineUrl, Sha256(payload), Sign(otherKey, payload)),
            }),
        });

        Assert.ThrowsAsync<EngineUpdateException>(() => CreateManager(manifest).EnsureEngine("1.0.0"));
    }

    [Test]
    public void InsecureVersionIsNotDownloaded()
    {
        var manifest = new EngineBuildManifest(new Dictionary<string, EngineVersionInfo>
        {
            ["1.0.0"] = new(Insecure: true, null, new Dictionary<string, EngineBuildInfo>
            {
                [RidSelector.Current] = new(EngineUrl, "00", "00"),
            }),
        });

        Assert.ThrowsAsync<EngineUpdateException>(() => CreateManager(manifest).EnsureEngine("1.0.0"));
    }

    [Test]
    public void NoBuildForPlatformThrowsOwnError()
    {
        var manifest = new EngineBuildManifest(new Dictionary<string, EngineVersionInfo>
        {
            ["1.0.0"] = new(false, null, new Dictionary<string, EngineBuildInfo>
            {
                ["solaris-sparc"] = new(EngineUrl, "00", "00"),
            }),
        });

        Assert.ThrowsAsync<NoEngineForPlatformException>(() => CreateManager(manifest).EnsureEngine("1.0.0"));
    }

    [Test]
    public async Task ModuleIsExtractedToDisk()
    {
        var zip = MakeZip(("file.txt", "содержимое"));
        _handler.Responses[ModuleUrl] = zip;

        var manager = CreateManager(BuildsWith("1.0.0", zip));
        var modules = ModulesWith("Robust.Client.WebView", "0.1.0", zip);

        var changed = await manager.EnsureModule("Robust.Client.WebView", "1.0.0", modules);

        var extracted = Path.Combine(_tempRoot, "modules", "Robust.Client.WebView", "0.1.0", "file.txt");
        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(File.Exists(extracted), Is.True);
            Assert.That(_store.HasModule("Robust.Client.WebView", "0.1.0"), Is.True);
        });
    }

    [Test]
    public async Task CullRemovesEverythingNotInUse()
    {
        var payload = "движок"u8.ToArray();
        _handler.Responses[EngineUrl] = payload;
        var manager = CreateManager(BuildsWith("1.0.0", payload));

        await manager.EnsureEngine("1.0.0");
        await manager.CullUnused([]);

        Assert.Multiple(() =>
        {
            Assert.That(_store.GetEngines(), Is.Empty);
            Assert.That(File.Exists(Path.Combine(_tempRoot, "engines", "1.0.0.zip")), Is.False);
        });
    }

    [Test]
    public async Task CullKeepsEngineNeededThroughRedirect()
    {
        var payload = "движок"u8.ToArray();
        _handler.Responses[EngineUrl] = payload;

        var builds = BuildsWith("1.0.1", payload);
        builds.Versions["1.0.0"] = new EngineVersionInfo(false, "1.0.1", []);

        var manager = CreateManager(builds);
        await manager.EnsureEngine("1.0.0");

        await manager.CullUnused([("Robust", "1.0.0")]);

        Assert.That(_store.FindEngine("1.0.1"), Is.Not.Null);
    }

    private EngineManager CreateManager(EngineBuildManifest builds, EngineModuleManifest? modules = null) =>
        new(
            _store,
            new StubManifestSource(builds, modules ?? new EngineModuleManifest([])),
            _http,
            new EngineSignature(_key.PublicKey),
            Path.Combine(_tempRoot, "engines"),
            Path.Combine(_tempRoot, "modules"));

    private EngineBuildManifest BuildsWith(string version, byte[] payload) =>
        new(new Dictionary<string, EngineVersionInfo>
        {
            [version] = new(false, null, new Dictionary<string, EngineBuildInfo>
            {
                [RidSelector.Current] = new(EngineUrl, Sha256(payload), Sign(_key, payload)),
            }),
        });

    private EngineModuleManifest ModulesWith(string name, string version, byte[] payload) =>
        new(new Dictionary<string, EngineModuleData>
        {
            [name] = new(new Dictionary<string, EngineModuleVersionData>
            {
                [version] = new(new Dictionary<string, EngineBuildInfo>
                {
                    [RidSelector.Current] = new(ModuleUrl, Sha256(payload), Sign(_key, payload)),
                }),
            }),
        });

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private static string Sign(Key key, byte[] data) =>
        Convert.ToHexString(SignatureAlgorithm.Ed25519.Sign(key, data));

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }

    private sealed class StubManifestSource(EngineBuildManifest builds, EngineModuleManifest modules)
        : IEngineManifestSource
    {
        public Task<EngineBuildManifest> GetBuilds(CancellationToken cancel = default) =>
            Task.FromResult(builds);

        public Task<EngineModuleManifest> GetModules(CancellationToken cancel = default) =>
            Task.FromResult(modules);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Responses { get; } = new();
        public Dictionary<string, int> RequestCount { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            RequestCount[url] = RequestCount.GetValueOrDefault(url) + 1;

            if (!Responses.TryGetValue(url, out var payload))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
        }
    }
}
