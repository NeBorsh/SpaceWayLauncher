using System.Runtime.InteropServices;

namespace SpaceWay.Core.Engine;

/// <summary>
/// Chooses which engine build to download for the current platform.
/// </summary>
public static class RidSelector
{
    /// <summary>
    /// Chains from exact match to fallbacks. A fallback is listed only where
    /// a foreign build actually runs: Windows runs x86 on x64, ARM macOS runs x64 via Rosetta.
    /// </summary>
    private static readonly Dictionary<string, string[]> Fallbacks = new()
    {
        ["win-x64"] = ["win-x64", "win-x86"],
        ["win-x86"] = ["win-x86"],
        ["win-arm64"] = ["win-arm64", "win-x64", "win-x86"],
        ["linux-x64"] = ["linux-x64"],
        ["linux-arm64"] = ["linux-arm64"],
        ["linux-arm"] = ["linux-arm"],
        ["osx-x64"] = ["osx-x64"],
        ["osx-arm64"] = ["osx-arm64", "osx-x64"],
    };

    /// <summary>RID of the current platform.</summary>
    public static string Current { get; } = GuessCurrentRid();

    /// <summary>
    /// Best available build, or null if nothing matches this platform.
    /// </summary>
    public static string? FindBest(IEnumerable<string> available, string? currentRid = null)
    {
        var set = available as ICollection<string> ?? available.ToList();
        var rid = currentRid ?? Current;

        if (!Fallbacks.TryGetValue(rid, out var chain))
            chain = [rid];

        return chain.FirstOrDefault(set.Contains);
    }

    private static string GuessCurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => "unknown",
        };

        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : "unknown";

        return $"{os}-{arch}";
    }
}
