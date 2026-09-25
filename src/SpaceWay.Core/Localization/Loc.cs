using System.Globalization;
using System.Reflection;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Shared.Types.Bundle;
using Serilog;

namespace SpaceWay.Core.Localization;

/// <summary>
/// Localization facade over Fluent (.ftl).
/// Fluent handles grammatical cases and plural forms with selectors
/// in the translation itself rather than in code.
/// </summary>
public static class Loc
{
    public const string DefaultLanguage = "en-US";

    private static FluentBundle? _bundle;
    private static bool _fallbackLoadFailed;
    private static string _current = DefaultLanguage;

    /// <summary>Languages that have translation files.</summary>
    public static IReadOnlyList<LanguageInfo> Available { get; } =
    [
        new("en-US", "English"),
        new("ru-RU", "Русский"),
    ];

    public static string Current => _current;

    /// <summary>
    /// Raised when the language changes, so the UI re-reads strings without a restart.
    /// </summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// Subscribers the event does not keep alive.
    /// </summary>
    private static readonly List<WeakReference<Action>> Weak = [];

    /// <summary>Number of live subscribers. Used by the leak test.</summary>
    public static int WeakListenerCount
    {
        get
        {
            lock (Weak)
                return Weak.Count(w => w.TryGetTarget(out _));
        }
    }

    /// <summary>
    /// Subscribes a handler without keeping its owner alive.
    /// </summary>
    public static void SubscribeWeak(Action handler)
    {
        lock (Weak)
            Weak.Add(new WeakReference<Action>(handler));
    }

    public static void UnsubscribeWeak(Action handler)
    {
        lock (Weak)
            Weak.RemoveAll(w => !w.TryGetTarget(out var target) || ReferenceEquals(target, handler));
    }

    public static void SetLanguage(string language)
    {
        var bundle = LoadBundle(language);
        if (bundle == null)
        {
            Log.Warning("Failed to load language {Language}, keeping {Current}", language, _current);
            return;
        }

        _bundle = bundle;
        _current = language;
        LanguageChanged?.Invoke();
        NotifyWeak();
    }

    /// <summary>
    /// Invokes weak subscribers, dropping collected ones along the way.
    /// </summary>
    private static void NotifyWeak()
    {
        Action[] alive;

        lock (Weak)
        {
            alive = [.. Weak.Select(w => w.TryGetTarget(out var target) ? target : null).OfType<Action>()];
            Weak.RemoveAll(w => !w.TryGetTarget(out _));
        }

        foreach (var handler in alive)
            handler();
    }

    /// <summary>
    /// Translates a string by key. Never throws and never returns an empty
    /// string: on any problem it returns the key itself, so a broken translation
    /// does not turn into an empty window.
    /// </summary>
    public static string T(string key, params (string Name, object Value)[] args)
    {
        if (_bundle == null && !_fallbackLoadFailed)
        {
            _bundle = LoadBundle(DefaultLanguage);
            _fallbackLoadFailed = _bundle == null;
        }

        var bundle = _bundle;
        if (bundle == null)
            return key;

        var fluentArgs = args.Length == 0 ? null : BuildArgs(args);

        if (bundle.TryGetMessage(key, fluentArgs, out _, out var value) && value != null)
            return value;

        Log.Debug("No translation for key {Key} in {Language}", key, _current);
        return key;
    }

    private static Dictionary<string, IFluentType> BuildArgs((string Name, object Value)[] args)
    {
        var result = new Dictionary<string, IFluentType>(args.Length);
        foreach (var (name, value) in args)
        {
            result[name] = value switch
            {
                string s => (FluentString)s,
                int i => (FluentNumber)i,
                long l => (FluentNumber)l,
                double d => (FluentNumber)d,
                _ => (FluentString)(value.ToString() ?? string.Empty),
            };
        }

        return result;
    }

    private static FluentBundle? LoadBundle(string language)
    {
        var resourceName = $"SpaceWay.Core.Localization.Locales.{language}.ftl";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Log.Warning("Translation file {Resource} not found", resourceName);
            return null;
        }

        using var reader = new StreamReader(stream);

        try
        {
            return LinguiniBuilder.Builder()
                .CultureInfo(new CultureInfo(language))
                .AddResource(reader.ReadToEnd())
                .UncheckedBuild();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to parse translation file {Language}", language);
            return null;
        }
    }
}

public sealed record LanguageInfo(string Code, string NativeName);
