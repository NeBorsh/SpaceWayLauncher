using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Mods;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ModImportTests
{
    private LauncherDatabase _db = null!;
    private string _tempRoot = null!;
    private string _modsDir = null!;
    private string _downloads = null!;
    private ModLibrary _library = null!;
    private ModImport _import = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        _tempRoot = TestPaths.CreateTempRoot("import");
        _modsDir = Path.Combine(_tempRoot, "mods");
        _downloads = Path.Combine(_tempRoot, "downloads");
        Directory.CreateDirectory(_modsDir);
        Directory.CreateDirectory(_downloads);

        _library = new ModLibrary(new ModStore(_db), _modsDir);
        _import = new ModImport(_library);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public void NewModIsCopiedAndEnabled()
    {
        var result = _import.Import(Download("Content.Atmos.dll", "v1"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.Added));
            Assert.That(File.ReadAllText(Path.Combine(_modsDir, "Content.Atmos.dll")), Is.EqualTo("v1"));
            Assert.That(_library.EnabledFiles(), Is.EqualTo(new[] { "Content.Atmos.dll" }));
        });
    }

    [Test]
    public void SameFileAgainChangesNothing()
    {
        _import.Import(Download("Content.Atmos.dll", "v1"));
        _library.SetEnabled("Content.Atmos.dll", false);

        var result = _import.Import(Download("Content.Atmos.dll", "v1"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.AlreadyPresent));

            Assert.That(_library.EnabledFiles(), Is.Empty);
        });
    }

    [Test]
    public void FileDroppedFromModsFolderItselfIsAlreadyPresent()
    {
        File.WriteAllText(Path.Combine(_modsDir, "Content.Atmos.dll"), "v1");

        var result = _import.Import(Path.Combine(_modsDir, "Content.Atmos.dll"));

        Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.AlreadyPresent));
    }

    [Test]
    public void DifferentContentUnderSameNameAsksFirst()
    {
        _import.Import(Download("Content.Atmos.dll", "v1"));

        var result = _import.Import(Download("Content.Atmos.dll", "v2"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.NeedsReplace));
            Assert.That(File.ReadAllText(Path.Combine(_modsDir, "Content.Atmos.dll")), Is.EqualTo("v1"));
        });
    }

    [Test]
    public void ReplaceKeepsPlayersChoice()
    {
        _import.Import(Download("Content.Atmos.dll", "v1"));
        _library.SetEnabled("Content.Atmos.dll", false);

        var result = _import.Import(Download("Content.Atmos.dll", "v2"), replace: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.Replaced));
            Assert.That(File.ReadAllText(Path.Combine(_modsDir, "Content.Atmos.dll")), Is.EqualTo("v2"));
            Assert.That(_library.EnabledFiles(), Is.Empty);
        });
    }

    [Test]
    public void NameDifferingOnlyInCaseReplacesTheSameMod()
    {
        _import.Import(Download("Content.Atmos.dll", "v1"));
        _library.SetEnabled("Content.Atmos.dll", false);
        _library.SetEnabled("Content.Atmos.dll", true);

        var result = _import.Import(Download("Content.ATMOS.dll", "v2"), replace: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.Replaced));
            Assert.That(result.FileName, Is.EqualTo("Content.Atmos.dll"));
            Assert.That(Directory.EnumerateFiles(_modsDir).Select(Path.GetFileName),
                Is.EqualTo(new[] { "Content.Atmos.dll" }));
            Assert.That(_library.EnabledFiles(), Is.EqualTo(new[] { "Content.Atmos.dll" }));
        });
    }

    [Test]
    public void SameModUnderAnotherNameIsNotCopied()
    {
        _import.Import(Download("Content.Atmos.dll", "v1"));

        var result = _import.Import(Download("Content.Atmos (1).dll", "v1"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.DuplicateOf));
            Assert.That(result.ExistingName, Is.EqualTo("Content.Atmos.dll"));
            Assert.That(File.Exists(Path.Combine(_modsDir, "Content.Atmos (1).dll")), Is.False);
        });
    }

    [TestCase("Content.Atmos.txt")]
    [TestCase("Content.Atmos.zip")]
    public void NotAnAssemblyIsRejected(string name)
    {
        var result = _import.Import(Download(name, "что-то"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.NotAssembly));
            Assert.That(Directory.EnumerateFiles(_modsDir), Is.Empty);
        });
    }

    [Test]
    public void FolderIsRejected()
    {
        var folder = Path.Combine(_downloads, "Content.Folder.dll");
        Directory.CreateDirectory(folder);

        Assert.That(_import.Import(folder).Outcome, Is.EqualTo(ModImportOutcome.NotAssembly));
    }

    [Test]
    public void NameWithoutPrefixIsRejected()
    {
        var result = _import.Import(Download("Atmos.dll", "v1"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ModImportOutcome.BadName));
            Assert.That(Directory.EnumerateFiles(_modsDir), Is.Empty);
        });
    }

    [Test]
    public void NoTemporaryFilesAreLeftBehind()
    {
        _import.Import(Download("Content.Atmos.dll", "v1"));
        _import.Import(Download("Content.Atmos.dll", "v2"), replace: true);

        Assert.That(Directory.EnumerateFiles(_modsDir).Select(Path.GetFileName),
            Is.EqualTo(new[] { "Content.Atmos.dll" }));
    }

    private string Download(string name, string content)
    {
        var path = Path.Combine(_downloads, name);
        File.WriteAllText(path, content);
        return path;
    }
}
