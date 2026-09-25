using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Mods;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ModLibraryTests
{
    private LauncherDatabase _db = null!;
    private string _tempRoot = null!;
    private string _modsDir = null!;
    private ModLibrary _library = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        _tempRoot = TestPaths.CreateTempRoot("library");
        _modsDir = Path.Combine(_tempRoot, "mods");
        Directory.CreateDirectory(_modsDir);

        _library = new ModLibrary(new ModStore(_db), _modsDir);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public void EmptyFolder_MeansNoMods()
    {
        Assert.That(_library.All(), Is.Empty);
    }

    [Test]
    public void FolderIsTheSourceOfTruth()
    {
        Put("Content.Atmos.dll");
        Put("Content.Pipes.dll");

        Assert.That(_library.All().Select(m => m.FileName), Is.EquivalentTo(new[]
        {
            "Content.Atmos.dll",
            "Content.Pipes.dll",
        }));
    }

    [Test]
    public void NewMod_IsOffByDefault()
    {
        Put("Content.Atmos.dll");

        Assert.That(_library.All().Single().Enabled, Is.False);
    }

    [Test]
    public void EnabledMods_KeepTheirOrder()
    {
        Put("Content.Zeta.dll");
        Put("Content.Alpha.dll");

        _library.SetEnabled("Content.Zeta.dll", true);
        _library.SetEnabled("Content.Alpha.dll", true);

        Assert.That(_library.EnabledFiles(), Is.EqualTo(new[]
        {
            "Content.Zeta.dll",
            "Content.Alpha.dll",
        }));
    }

    [Test]
    public void DisappearedMod_IsShownAsMissing()
    {
        Put("Content.Atmos.dll");
        _library.SetEnabled("Content.Atmos.dll", true);

        File.Delete(Path.Combine(_modsDir, "Content.Atmos.dll"));

        var entry = _library.All().Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Missing, Is.True);
            Assert.That(entry.Enabled, Is.True);
            Assert.That(entry.IsUsable, Is.False);
        });
    }

    [Test]
    public void WrongName_IsMarkedUnusable()
    {
        Put("Atmos.dll");

        Assert.That(_library.All().Single().IsUsable, Is.False);
    }

    [Test]
    public void ChoiceSurvivesRestart()
    {
        Put("Content.Atmos.dll");
        _library.SetEnabled("Content.Atmos.dll", true);

        var reopened = new ModLibrary(new ModStore(_db), _modsDir);

        Assert.That(reopened.EnabledFiles(), Is.EqualTo(new[] { "Content.Atmos.dll" }));
    }

    private void Put(string name) =>
        File.WriteAllText(Path.Combine(_modsDir, name), "не настоящая сборка");
}
