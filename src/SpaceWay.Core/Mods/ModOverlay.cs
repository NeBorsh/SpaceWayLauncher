using System.IO.Compression;
using Serilog;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Mods;

/// <summary>
/// Builds an overlay zip from enabled mods.
/// </summary>
public sealed class ModOverlay(ModLibrary library, string? overlayDir = null)
{
    /// <summary>Path inside the zip where the engine looks for content assemblies.</summary>
    private const string AssembliesPath = "Assemblies/";

    /// <summary>
    /// The engine only loads files with this prefix from <c>/Assemblies/</c>.
    /// A mod named otherwise is silently ignored.
    /// </summary>
    public const string RequiredPrefix = "Content.";

    /// <summary>How many zips from previous launches to keep.</summary>
    private const int KeptOverlays = 5;

    private readonly string _overlayDir = overlayDir ?? LauncherPaths.DirOverlays;

    /// <summary>
    /// Prepares the overlay, or returns null if no mods are enabled.
    /// </summary>
    public string? Build(DateTimeOffset now)
    {
        var enabled = library.EnabledFiles();
        if (enabled.Count == 0)
            return null;

        var files = enabled
            .Select(name => (Name: name, Path: Path.Combine(library.Directory, name)))
            .ToList();

        foreach (var (name, path) in files)
        {
            if (!File.Exists(path))
                throw new ModException("error-mod-file-missing", ("file", name));

            if (!name.StartsWith(RequiredPrefix, StringComparison.Ordinal))
                throw new ModException("error-mod-bad-name", ("file", name), ("prefix", RequiredPrefix));
        }

        FileHelpers.EnsureDirectoryExists(_overlayDir);
        CullOldOverlays();

        var stamp = now.ToLocalTime().ToString("yyyyMMdd-HHmmss");
        var overlayPath = Path.Combine(_overlayDir, $"overlay-{stamp}.zip");

        using (var zip = ZipFile.Open(overlayPath, ZipArchiveMode.Create))
        {
            foreach (var (name, path) in files)
            {
                zip.CreateEntryFromFile(path, AssembliesPath + name);
            }
        }

        Log.Information("Overlay built, mods: {Count}", files.Count);

        return overlayPath;
    }

    /// <summary>
    /// Removes zips from previous launches.
    /// </summary>
    private void CullOldOverlays()
    {
        var old = Directory.EnumerateFiles(_overlayDir, "overlay-*.zip")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(KeptOverlays);

        foreach (var path in old)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>Building the overlay failed for a reason that can be shown to the player.</summary>
public sealed class ModException(string key, params (string Name, object Value)[] args)
    : LocalizedException(key, null, args);
