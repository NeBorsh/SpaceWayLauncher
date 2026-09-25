namespace SpaceWay.Core.Util;

/// <summary>Disk space used by downloaded data.</summary>
/// <param name="ContentBytes">Content database with all versions.</param>
/// <param name="EngineBytes">Engine archives and extracted modules.</param>
public readonly record struct StorageReport(long ContentBytes, long EngineBytes)
{
    public long Total => ContentBytes + EngineBytes;
}

/// <summary>
/// Measures used disk space.
/// </summary>
public static class LauncherStorage
{
    public static StorageReport Measure() => new(
        FileSize(LauncherPaths.PathContentDb)
        + FileSize(LauncherPaths.PathContentDb + "-wal")
        + FileSize(LauncherPaths.PathContentDb + "-shm"),
        DirectorySize(LauncherPaths.DirEngineInstallations)
        + DirectorySize(LauncherPaths.DirModuleInstallations));

    private static long FileSize(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length : 0;
    }

    private static long DirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long total = 0;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        return total;
    }
}
