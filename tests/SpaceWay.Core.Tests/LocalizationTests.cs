using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Localization;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Translations break silently: Fluent rejects the whole file because of
/// a single duplicate entry, and keys appear in the window instead of text.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
    private static readonly Regex MessageKey = new(@"^([a-z0-9-]+) = ", RegexOptions.Multiline);

    [Test]
    [TestCaseSource(nameof(Languages))]
    public void TranslationFile_Loads(string language)
    {
        Loc.SetLanguage(language);

        Assert.That(Loc.T("app-name"), Is.Not.EqualTo("app-name"),
            $"translation file {language} failed to parse");
    }

    [Test]
    [TestCaseSource(nameof(Languages))]
    public void KeysAreNotDuplicated(string language)
    {
        var keys = ReadKeys(language);
        var duplicates = keys.GroupBy(k => k)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.That(duplicates, Is.Empty, "a duplicate key breaks the whole translation file");
    }

    [Test]
    public void KeySets_MatchAcrossLanguages()
    {
        var reference = ReadKeys(Loc.DefaultLanguage).ToHashSet();

        foreach (var language in Languages.Where(l => l != Loc.DefaultLanguage))
        {
            var keys = ReadKeys(language).ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(reference.Except(keys), Is.Empty,
                    $"{language} is missing keys present in {Loc.DefaultLanguage}");
                Assert.That(keys.Except(reference), Is.Empty,
                    $"{language} has extra keys");
            });
        }
    }

    [Test]
    public void ErrorMessageFollowsLanguage()
    {
        var error = new ConnectException("error-loader-missing", null, ("path", "loader.exe"));

        Loc.SetLanguage("ru-RU");
        var russian = error.Message;

        Loc.SetLanguage("en-US");
        var english = error.Message;

        Assert.Multiple(() =>
        {
            Assert.That(russian, Is.Not.EqualTo(error.Key), "key has no translation");
            Assert.That(english, Is.Not.EqualTo(error.Key), "key has no translation");
            Assert.That(russian, Is.Not.EqualTo(english));
            Assert.That(english, Does.Contain("loader.exe"), "подстановка не сработала");
        });
    }

    [Test]
    [TestCaseSource(nameof(Languages))]
    public void PluralFormsResolve(string language)
    {
        Loc.SetLanguage(language);

        foreach (var count in (int[])[0, 1, 2, 5, 21])
        {
            var text = Loc.T("accounts-server-remove-text", ("server", "Auth"), ("count", count));

            Assert.That(text, Is.Not.EqualTo("accounts-server-remove-text"),
                $"no variant matched {count}");
            Assert.That(Loc.T("accounts-server-accounts", ("count", count)),
                Is.Not.EqualTo("accounts-server-accounts"),
                $"no variant matched {count}");
        }
    }

    [Test]
    public void CollectedListeners_StopBeingCalled()
    {
        var before = Loc.WeakListenerCount;

        CreateListeners(50);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Loc.SetLanguage(Loc.DefaultLanguage);

        Assert.That(Loc.WeakListenerCount, Is.EqualTo(before));
    }

    [Test]
    public void LiveListener_IsStillCalled()
    {
        var listener = new Listener();
        Loc.SubscribeWeak(listener.Handler);

        try
        {
            GC.Collect();
            Loc.SetLanguage("ru-RU");

            Assert.That(listener.Calls, Is.GreaterThan(0), "a live subscriber was not called");
        }
        finally
        {
            Loc.UnsubscribeWeak(listener.Handler);
        }
    }

    /// <summary>
    /// Creates subscribers and immediately forgets them, as the server list
    /// forgets cards on the next filter.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateListeners(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var listener = new Listener();
            Loc.SubscribeWeak(listener.Handler);
        }
    }

    /// <summary>Subscriber holding its delegate in a field, like LocalizedViewModel.</summary>
    private sealed class Listener
    {
        public Listener() => Handler = () => Calls++;

        public Action Handler { get; }

        public int Calls { get; private set; }
    }

    [Test]
    public void EveryKeyIsUsedSomewhere()
    {
        var sources = FindSourceRoot();

        if (sources == null)
        {
            Assert.Ignore("sources not found nearby");
            return;
        }

        var text = string.Concat(Directory
            .EnumerateFiles(sources, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".axaml"))
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));

        var orphans = ReadKeys(Loc.DefaultLanguage).Where(key => !text.Contains(key)).ToList();

        Assert.That(orphans, Is.Empty, "keys nobody uses");
    }

    /// <summary>Finds the src directory by walking up from the test binaries.</summary>
    private static string? FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    private static IEnumerable<string> Languages => Loc.Available.Select(l => l.Code);

    private static List<string> ReadKeys(string language)
    {
        var assembly = typeof(Loc).Assembly;
        var name = $"SpaceWay.Core.Localization.Locales.{language}.ftl";

        using var stream = assembly.GetManifestResourceStream(name)
                           ?? throw new FileNotFoundException($"missing resource {name}");
        using var reader = new StreamReader(stream);

        return MessageKey.Matches(reader.ReadToEnd())
            .Select(m => m.Groups[1].Value)
            .ToList();
    }
}
