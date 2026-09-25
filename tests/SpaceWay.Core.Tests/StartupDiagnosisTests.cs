using NUnit.Framework;
using SpaceWay.Core.Connecting;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Lines taken from a real crash: an example mod threw FormatException,
/// which is not on the sandbox whitelist.
/// </summary>
[TestFixture]
public sealed class StartupDiagnosisTests
{
    private const string RealStdout = """
        [DEBG] res.mod: ENABLING sandboxing
        [DEBG] res.mod: Found module '/Assemblies/Content.SpaceWayBrainfuck.dll'
        [DEBG] res.typecheck: Content.SpaceWayBrainfuck: Verified IL in 61.0743ms
        [ERRO] res.typecheck: Sandbox violation: Access to type not allowed: [System.Runtime]System.FormatException
        """;

    private const string RealStderr = """
        Unhandled exception. System.AggregateException: One or more errors occurred. (Assembly Content.SpaceWayBrainfuck failed type checks.)
         ---> Robust.Shared.ContentPack.TypeCheckFailedException: Assembly Content.SpaceWayBrainfuck failed type checks.
           at Robust.Shared.ContentPack.ModLoader.TryLoadModules(IEnumerable`1 paths)
        """;

    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = TestPaths.CreateTempRoot("diagnosis");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown() => TestPaths.DeleteQuietly(_tempRoot);

    [Test]
    public void SandboxViolation_NamesAssemblyAndType()
    {
        var failure = StartupDiagnosis.Diagnose(
            Write("stderr.log", RealStderr), Write("stdout.log", RealStdout));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Kind, Is.EqualTo(StartupFailureKind.ModRejected));
            Assert.That(failure.Assembly, Is.EqualTo("Content.SpaceWayBrainfuck"));

            Assert.That(failure.Type, Is.EqualTo("System.FormatException"));
        });
    }

    [Test]
    public void ViolationAlone_IsEnough()
    {
        var failure = StartupDiagnosis.Diagnose(Write("stdout.log", RealStdout));

        Assert.Multiple(() =>
        {
            Assert.That(failure.Kind, Is.EqualTo(StartupFailureKind.ModRejected));
            Assert.That(failure.Type, Is.EqualTo("System.FormatException"));
            Assert.That(failure.Assembly, Is.Null);
        });
    }

    [Test]
    public void OrdinaryCrash_StaysUnknown()
    {
        var failure = StartupDiagnosis.Diagnose(Write("stderr.log", """
            Unhandled exception. System.NullReferenceException: Object reference not set.
               at Content.Client.Entry.EntryPoint.PostInit()
            """));

        Assert.That(failure.Kind, Is.EqualTo(StartupFailureKind.Unknown));
    }

    [Test]
    public void MissingLog_IsNotAFailureOfItsOwn()
    {
        var failure = StartupDiagnosis.Diagnose(
            Path.Combine(_tempRoot, "которого-нет.log"), null);

        Assert.That(failure.Kind, Is.EqualTo(StartupFailureKind.Unknown));
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_tempRoot, name);
        File.WriteAllText(path, text);

        return path;
    }
}
