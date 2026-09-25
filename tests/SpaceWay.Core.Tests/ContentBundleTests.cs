using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SpaceWay.Core.Content;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ContentBundleTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = TestPaths.CreateTempRoot("bundle");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown() => TestPaths.DeleteQuietly(_tempRoot);

    [Test]
    public void ReplayMountsOverBaseBuild()
    {
        var path = Write(_tempRoot, "replay.zip", ReplayMetadata, ("replay/data.yml", "раунд"));

        using var bundle = ContentBundle.Open(path);

        Assert.Multiple(() =>
        {
            Assert.That(bundle.MountsAsOverlay, Is.True);
            Assert.That(bundle.Metadata.EngineVersion, Is.EqualTo("290.0.0"));
            Assert.That(bundle.Metadata.ServerGC, Is.True);
            Assert.That(bundle.Metadata.BaseBuild!.ForkId, Is.EqualTo("wizards"));
            Assert.That(bundle.Metadata.BaseBuildInformation().ManifestHash, Is.EqualTo("AABB"));
        });
    }

    [Test]
    public void BundleWithOwnManifestGoesIntoDatabase()
    {
        var path = Write(_tempRoot, "bundle.zip", ReplayMetadata, ("manifest.yml", "modules: []"));

        using var bundle = ContentBundle.Open(path);

        Assert.That(bundle.MountsAsOverlay, Is.False);
    }

    [Test]
    public void BundleWithoutBaseBuildGoesIntoDatabase()
    {
        var path = Write(_tempRoot, "bundle.zip", """{ "engine_version": "290.0.0" }""");

        using var bundle = ContentBundle.Open(path);

        Assert.Multiple(() =>
        {
            Assert.That(bundle.MountsAsOverlay, Is.False);
            Assert.That(bundle.Metadata.BaseBuild, Is.Null);
        });
    }

    [Test]
    public void OrdinaryZipIsNotABundle()
    {
        var path = Write(_tempRoot, "photos.zip", metadata: null, ("cat.png", "мяу"));

        var error = Assert.Throws<ContentUpdateException>(() => ContentBundle.Open(path));

        Assert.That(error!.Key, Is.EqualTo("error-not-a-bundle"));
    }

    [Test]
    public void NotAZipAtAllIsReportedClearly()
    {
        var path = Path.Combine(_tempRoot, "replay.zip");
        File.WriteAllText(path, "это переименованный rar");

        var error = Assert.Throws<ContentUpdateException>(() => ContentBundle.Open(path));

        Assert.That(error!.Key, Is.EqualTo("error-bundle-unreadable"));
    }

    [Test]
    public void BrokenMetadataIsReportedClearly()
    {
        var path = Write(_tempRoot, "replay.zip", "{ не json");

        var error = Assert.Throws<ContentUpdateException>(() => ContentBundle.Open(path));

        Assert.That(error!.Key, Is.EqualTo("error-bundle-bad-metadata"));
    }

    [Test]
    public void MetadataWithoutEngineVersionIsRejected()
    {
        var path = Write(_tempRoot, "replay.zip", "{}");

        var error = Assert.Throws<ContentUpdateException>(() => ContentBundle.Open(path));

        Assert.That(error!.Key, Is.EqualTo("error-bundle-bad-metadata"));
    }

    [Test]
    public void FileIsReleasedWhenOpeningFails()
    {
        var path = Write(_tempRoot, "photos.zip", metadata: null, ("cat.png", "мяу"));

        Assert.Throws<ContentUpdateException>(() => ContentBundle.Open(path));

        Assert.DoesNotThrow(() => File.Delete(path));
    }

    [Test]
    public void KeyDiffersFromPlainZipHash()
    {
        var path = Write(_tempRoot, "bundle.zip", ReplayMetadata);

        using var bundle = ContentBundle.Open(path);
        var plain = SHA256.HashData(File.ReadAllBytes(path));

        Assert.Multiple(() =>
        {
            Assert.That(bundle.ComputeKey(), Is.Not.EqualTo(plain));
            Assert.That(bundle.ComputeKey(), Is.EqualTo(bundle.ComputeKey()));
        });
    }

    internal const string ReplayMetadata = """
        {
          "engine_version": "290.0.0",
          "server_gc": true,
          "base_build": {
            "fork_id": "wizards",
            "version": "abc123",
            "manifest_url": "https://cdn.example/manifest",
            "manifest_download_url": "https://cdn.example/download",
            "manifest_hash": "AABB"
          }
        }
        """;

    internal static string Write(string dir, string name, string? metadata, params (string Path, string Content)[] files)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        if (metadata != null)
            Add(ContentBundle.MetadataFile, metadata);

        foreach (var (file, content) in files)
            Add(file, content);

        return path;

        void Add(string entryName, string content)
        {
            using var stream = zip.CreateEntry(entryName).Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }
    }
}
