using SpaceWay.Core.Data;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Mods;

/// <summary>
/// A mod: an assembly file in the mods folder and whether the player enabled it.
/// </summary>
/// <param name="FileName">File name, also shown as the mod name in the UI.</param>
public sealed record ModEntry(string FileName, bool Enabled, long Size, bool Missing = false)
{
    /// <summary>
    /// Whether the engine will load this file.
    /// </summary>
    public bool IsUsable =>
        !Missing && FileName.StartsWith(ModOverlay.RequiredPrefix, StringComparison.Ordinal);
}

/// <summary>
/// Mods in the mods folder.
/// </summary>
public sealed class ModLibrary(ModStore store, string? modsDir = null)
{
    public string Directory { get; } = modsDir ?? LauncherPaths.DirMods;

    /// <summary>Raised when the player enables or disables a mod.</summary>
    public event Action? Changed;

    /// <summary>
    /// All mods in the folder.
    /// </summary>
    public IReadOnlyList<ModEntry> All()
    {
        var enabled = store.GetEnabled();

        var files = System.IO.Directory.Exists(Directory)
            ? System.IO.Directory.EnumerateFiles(Directory, "*.dll").Select(Path.GetFileName)
            : [];

        var found = files.OfType<string>().ToList();

        var known = enabled
            .Select(name => new ModEntry(name, true, SizeOf(name), !found.Contains(name)));

        var rest = found
            .Where(name => !enabled.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new ModEntry(name, false, SizeOf(name)));

        return [.. known, .. rest];
    }

    /// <summary>Enabled mods in load order, excluding missing files.</summary>
    public IReadOnlyList<string> EnabledFiles() =>
        [.. All().Where(m => m.Enabled).Select(m => m.FileName)];

    public void SetEnabled(string fileName, bool enabled)
    {
        if (enabled)
            store.Enable(fileName);
        else
            store.Disable(fileName);

        Changed?.Invoke();
    }

    private long SizeOf(string fileName)
    {
        var info = new FileInfo(Path.Combine(Directory, fileName));

        return info.Exists ? info.Length : 0;
    }
}
