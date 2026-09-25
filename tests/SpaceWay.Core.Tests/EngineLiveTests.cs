using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Checks against the real Space Wizards CDN.
/// </summary>
[TestFixture]
[Explicit("Требует сети и качает настоящую сборку движка")]
[Category("Live")]
public sealed class EngineLiveTests
{
    [Test]
    public async Task BuildManifestParses()
    {
        using var http = new HttpClient();
        var manifest = await new RobustBuildsApi(http).GetBuilds();

        Assert.That(manifest.Versions, Is.Not.Empty);

        var found = manifest.Find(NewestVersion(manifest));

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Info.Platforms, Is.Not.Empty);
            Assert.That(RidSelector.FindBest(found.Info.Platforms.Keys), Is.Not.Null,
                $"no build for {RidSelector.Current}");
        });

        TestContext.Out.WriteLine($"Latest engine version: {found!.Version}");
    }

    [Test]
    public async Task ModuleManifestParses()
    {
        using var http = new HttpClient();
        var manifest = await new RobustBuildsApi(http).GetModules();

        Assert.That(manifest.Modules.Keys, Does.Contain("Robust.Client.WebView"));
    }

    [Test]
    public async Task RealBuildPassesSignatureCheck()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var db = LauncherDatabase.CreateInMemory();

        var root = Path.Combine(Path.GetTempPath(), $"spaceway-live-{Guid.NewGuid():N}");
        var api = new RobustBuildsApi(http);
        var manager = new EngineManager(
            new EngineStore(db), api, http,
            engineDir: Path.Combine(root, "engines"),
            moduleDir: Path.Combine(root, "modules"));

        try
        {
            var result = await manager.EnsureEngine(NewestVersion(await api.GetBuilds()));

            Assert.That(result.Changed, Is.True);
            TestContext.Out.WriteLine($"Downloaded and verified engine {result.Version}");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Latest engine version from the manifest.
    /// </summary>
    private static string NewestVersion(EngineBuildManifest manifest) => manifest.Versions.Keys
        .Select(key => (Key: key, Parsed: Version.TryParse(key, out var v) ? v : null))
        .Where(v => v.Parsed != null)
        .MaxBy(v => v.Parsed)
        .Key;
}
