using System.Text.RegularExpressions;
using Serilog;

namespace SpaceWay.Core.Connecting;

/// <summary>Why the game closed right after starting.</summary>
public enum StartupFailureKind
{
    /// <summary>No cause found in the log; a generic message is shown.</summary>
    Unknown,

    /// <summary>The engine sandbox rejected a mod assembly.</summary>
    ModRejected,
}

/// <summary>
/// Parsed crash cause.
/// </summary>
/// <param name="Assembly">Assembly rejected by the engine.</param>
/// <param name="Type">Type that caused the rejection.</param>
public sealed record StartupFailure(
    StartupFailureKind Kind,
    string? Assembly = null,
    string? Type = null);

/// <summary>
/// Reads the game log to find out why it died on startup.
/// </summary>
public static class StartupDiagnosis
{
    /// <summary>
    /// How many bytes of the log to read.
    /// </summary>
    private const int ReadLimit = 2 * 1024 * 1024;

    /// <summary>The engine reports a forbidden type as <c>[Assembly]Namespace.Type</c>.</summary>
    private static readonly Regex Violation = new(
        @"Sandbox violation: Access to type not allowed: \[[^\]]+\](?<type>[\w.+`]+)",
        RegexOptions.Compiled);

    private static readonly Regex FailedAssembly = new(
        @"Assembly (?<assembly>[\w.]+) failed type checks",
        RegexOptions.Compiled);

    public static StartupFailure Diagnose(params string?[] logPaths)
    {
        string? type = null;
        string? assembly = null;

        foreach (var path in logPaths)
        {
            var text = ReadHead(path);
            if (text == null)
                continue;

            type ??= Violation.Match(text) is { Success: true } m ? m.Groups["type"].Value : null;
            assembly ??= FailedAssembly.Match(text) is { Success: true } a
                ? a.Groups["assembly"].Value
                : null;
        }

        if (type == null && assembly == null)
            return new StartupFailure(StartupFailureKind.Unknown);

        return new StartupFailure(StartupFailureKind.ModRejected, assembly, type);
    }

    private static string? ReadHead(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            var buffer = new char[ReadLimit];
            var read = reader.Read(buffer, 0, buffer.Length);

            return new string(buffer, 0, read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Debug(e, "Failed to read game log {Path}", path);
            return null;
        }
    }
}
