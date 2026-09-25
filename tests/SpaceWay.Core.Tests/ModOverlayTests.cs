using System.IO.Compression;
using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Mods;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ModOverlayTests
{
    private LauncherDatabase _db = null!;
    private ModLibrary _library = null!;
    private string _tempRoot = null!;
    private string _modsDir = null!;
    private ModOverlay _overlay = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();

        _tempRoot = TestPaths.CreateTempRoot("mods");
        _modsDir = Path.Combine(_tempRoot, "mods");
        Directory.CreateDirectory(_modsDir);

        _library = new ModLibrary(new ModStore(_db), _modsDir);
        _overlay = new ModOverlay(_library, Path.Combine(_tempRoot, "overlays"));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public void WithoutEnabledMods_ThereIsNoOverlay()
    {
        PutMod("Content.Atmos.dll");

        Assert.That(_overlay.Build(DateTimeOffset.UtcNow), Is.Null);
    }

    [Test]
    public void EnabledMods_GoIntoAssembliesFolder()
    {
        Enable("Content.Atmos.dll", "Content.Pipes.dll");

        var path = _overlay.Build(DateTimeOffset.UtcNow);

        Assert.That(path, Is.Not.Null);

        using var zip = ZipFile.OpenRead(path!);

        Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[]
        {
            "Assemblies/Content.Atmos.dll",
            "Assemblies/Content.Pipes.dll",
        }));
    }

    [Test]
    public void DisabledMod_StaysOutOfOverlay()
    {
        Enable("Content.Atmos.dll", "Content.Pipes.dll");
        _library.SetEnabled("Content.Pipes.dll", false);

        using var zip = ZipFile.OpenRead(_overlay.Build(DateTimeOffset.UtcNow)!);

        Assert.That(
            zip.Entries.Select(e => e.FullName),
            Is.EquivalentTo(new[] { "Assemblies/Content.Atmos.dll" }));
    }

    [Test]
    public void MissingAssembly_IsRefusedByName()
    {
        PutMod("Content.Пропадёт.dll");
        _library.SetEnabled("Content.Пропадёт.dll", true);
        File.Delete(Path.Combine(_modsDir, "Content.Пропадёт.dll"));

        var error = Assert.Throws<ModException>(() => _overlay.Build(DateTimeOffset.UtcNow));

        Assert.That(error!.Key, Is.EqualTo("error-mod-file-missing"));
    }

    [Test]
    public void SecondLaunch_GetsItsOwnZip()
    {
        Enable("Content.Atmos.dll");

        var first = _overlay.Build(DateTimeOffset.UtcNow);
        var second = _overlay.Build(DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(File.Exists(first!), Is.True);
    }

    private void PutMod(string name) =>
        File.WriteAllText(Path.Combine(_modsDir, name), "не настоящая сборка");

    private void Enable(params string[] names)
    {
        foreach (var name in names)
        {
            PutMod(name);
            _library.SetEnabled(name, true);
        }
    }
}
