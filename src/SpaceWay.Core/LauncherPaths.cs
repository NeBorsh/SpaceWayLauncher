namespace SpaceWay.Core;

/// <summary>
/// Launcher data paths. The directory is separate from the official launcher's,
/// since its database schema may change at any time.
/// </summary>
public static class LauncherPaths
{
    private const string AppDirName = "SpaceWayLauncher";

    /// <summary>
    /// Portable mode: a <c>portable</c> file next to the executable keeps all data
    /// in a <c>data</c> folder beside it instead of the user profile.
    /// </summary>
    public static bool IsPortable { get; } =
        File.Exists(Path.Combine(AppContext.BaseDirectory, "portable"));

    /// <summary>
    /// Installed by the Windows installer, which leaves its uninstaller next to the launcher.
    /// Only such copies update themselves.
    /// </summary>
    public static bool IsInstalled { get; } = OperatingSystem.IsWindows()
        && !IsPortable
        && File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>Settings, favorites, accounts.</summary>
    public static string DirUserData { get; } = GetUserDataDir();

    /// <summary>Downloaded engines, content, logs. May be large.</summary>
    public static string DirLocalData { get; } = GetLocalDataDir();

    public static string DirEngineInstallations { get; } = Path.Combine(DirLocalData, "engines");
    public static string DirModuleInstallations { get; } = Path.Combine(DirLocalData, "modules");
    public static string DirLogs { get; } = Path.Combine(DirLocalData, "logs");

    /// <summary>Downloaded launcher installers.</summary>
    public static string DirUpdates { get; } = Path.Combine(DirLocalData, "updates");

    /// <summary>User mod assemblies.</summary>
    public static string DirMods { get; } = Path.Combine(DirUserData, "mods");

    /// <summary>
    /// Overlay zips built for each game launch.
    /// </summary>
    public static string DirOverlays { get; } = Path.Combine(DirLocalData, "overlays");

    public static string PathDataDb { get; } = Path.Combine(DirUserData, "settings.db");

    /// <summary>
    /// The game loader, a separate executable next to the launcher.
    /// </summary>
    public static string PathLoader { get; } = Path.Combine(
        AppContext.BaseDirectory,
        OperatingSystem.IsWindows() ? "SpaceWay.Loader.exe" : "SpaceWay.Loader");

    public static string PathContentDb { get; } = Path.Combine(DirLocalData, "content.db");

    /// <summary>
    /// Ed25519 public key for verifying engine build signatures.
    /// The key belongs to Space Wizards, who sign official Robust builds.
    /// </summary>
    public static string PathEnginePublicKey { get; } =
        Path.Combine(AppContext.BaseDirectory, "signing_key");

    /// <summary>
    /// File names for the running game's output.
    /// </summary>
    public static (string Stdout, string Stderr) ClientLogPaths(DateTimeOffset when)
    {
        var stamp = when.ToLocalTime().ToString("yyyyMMdd-HHmmss");

        return (
            Path.Combine(DirLogs, $"client-{stamp}.stdout.log"),
            Path.Combine(DirLogs, $"client-{stamp}.stderr.log"));
    }

    /// <summary>Game logs from previous runs, newest first.</summary>
    public static IEnumerable<string> ClientLogFiles()
    {
        if (!Directory.Exists(DirLogs))
            return [];

        return Directory.EnumerateFiles(DirLogs, "client-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc);
    }

    private static string GetUserDataDir()
    {
        if (IsPortable)
            return Path.Combine(AppContext.BaseDirectory, "data");

        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDirName);

        if (OperatingSystem.IsMacOS())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", AppDirName);

        return Path.Combine(XdgDir("XDG_CONFIG_HOME", ".config"), AppDirName);
    }

    private static string GetLocalDataDir()
    {
        if (IsPortable)
            return Path.Combine(AppContext.BaseDirectory, "data");

        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppDirName);

        if (OperatingSystem.IsMacOS())
            return GetUserDataDir();

        return Path.Combine(XdgDir("XDG_DATA_HOME", Path.Combine(".local", "share")), AppDirName);
    }

    private static string XdgDir(string envVar, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(value))
            return value;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
    }
}
