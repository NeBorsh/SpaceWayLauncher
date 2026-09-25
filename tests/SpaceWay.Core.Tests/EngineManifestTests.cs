using NUnit.Framework;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class EngineManifestTests
{
    [Test]
    public void RedirectResolvesToFinalVersion()
    {
        var manifest = Builds(
            ("1.0.0", Redirect("1.0.1")),
            ("1.0.1", Redirect("1.0.2")),
            ("1.0.2", Plain()));

        var found = manifest.Find("1.0.0");

        Assert.That(found!.Version, Is.EqualTo("1.0.2"));
    }

    [Test]
    public void RedirectCycleDoesNotHang()
    {
        var manifest = Builds(("1.0.0", Redirect("1.0.1")), ("1.0.1", Redirect("1.0.0")));

        Assert.Throws<EngineUpdateException>(() => manifest.Find("1.0.0"));
    }

    [Test]
    public void UnknownVersionReturnsNull()
    {
        Assert.That(Builds(("1.0.0", Plain())).Find("2.0.0"), Is.Null);
    }

    [Test]
    public void ModuleVersionPicksNewestSuitable()
    {
        var manifest = Modules("WebView", "0.1.0", "0.50.0", "0.100.0");

        Assert.That(manifest.ResolveVersion("WebView", "0.60.0"), Is.EqualTo("0.50.0"));
    }

    [Test]
    public void ModuleVersionMatchingEngineIsAccepted()
    {
        var manifest = Modules("WebView", "0.50.0", "0.100.0");

        Assert.That(manifest.ResolveVersion("WebView", "0.100.0"), Is.EqualTo("0.100.0"));
    }

    [Test]
    public void ModuleVersionThrowsWhenEngineTooOld()
    {
        var manifest = Modules("WebView", "0.50.0");

        Assert.Throws<EngineUpdateException>(() => manifest.ResolveVersion("WebView", "0.10.0"));
    }

    [Test]
    public void UnknownModuleThrows()
    {
        var manifest = Modules("WebView", "0.50.0");

        Assert.Throws<EngineUpdateException>(() => manifest.ResolveVersion("Другой", "1.0.0"));
    }

    private static EngineBuildManifest Builds(params (string Version, EngineVersionInfo Info)[] versions) =>
        new(versions.ToDictionary(v => v.Version, v => v.Info));

    private static EngineVersionInfo Redirect(string to) => new(false, to, []);

    private static EngineVersionInfo Plain() => new(false, null, new Dictionary<string, EngineBuildInfo>
    {
        ["win-x64"] = new("https://example/engine.zip", "00", "00"),
    });

    private static EngineModuleManifest Modules(string name, params string[] versions) =>
        new(new Dictionary<string, EngineModuleData>
        {
            [name] = new(versions.ToDictionary(
                v => v,
                _ => new EngineModuleVersionData(new Dictionary<string, EngineBuildInfo>
                {
                    ["win-x64"] = new("https://example/module.zip", "00", "00"),
                }))),
        });
}
