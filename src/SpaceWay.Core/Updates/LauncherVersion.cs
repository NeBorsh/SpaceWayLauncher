using System.Diagnostics.CodeAnalysis;

namespace SpaceWay.Core.Updates;

/// <summary>Launcher version as <c>MAJOR.MINOR.PATCH</c>.</summary>
public static class LauncherVersion
{
    public static Version Current { get; } =
        Normalize(typeof(LauncherVersion).Assembly.GetName().Version ?? new Version(0, 0, 0));

    public static string CurrentText => Current.ToString(3);

    /// <summary>
    /// Parses a release tag such as <c>0.2.0</c> or <c>v0.2.0</c>.
    /// Suffixes after <c>-</c> or <c>+</c> are ignored.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out Version? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();

        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        var suffix = trimmed.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            trimmed = trimmed[..suffix];

        if (!Version.TryParse(trimmed, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
