using System.Security.Cryptography;
using Serilog;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Mods;

/// <summary>Outcome of adding a file to mods.</summary>
public enum ModImportOutcome
{
    /// <summary>New mod, copied and enabled.</summary>
    Added,

    /// <summary>Replaced a mod with the same name and different contents.</summary>
    Replaced,

    /// <summary>A byte-identical mod with the same name already exists. Nothing done.</summary>
    AlreadyPresent,

    /// <summary>The same mod already exists under a different name. Not copied.</summary>
    DuplicateOf,

    /// <summary>A different mod with the same name exists. Replacing is up to the player.</summary>
    NeedsReplace,

    /// <summary>Not an assembly: not a <c>.dll</c> or not a file at all.</summary>
    NotAssembly,

    /// <summary>Name lacks the <c>Content.</c> prefix, so the engine will not load it.</summary>
    BadName,
}

/// <param name="FileName">Name of the file being added.</param>
/// <param name="ExistingName">For <see cref="ModImportOutcome.DuplicateOf"/>: the name the mod already exists under.</param>
public sealed record ModImportResult(string FileName, ModImportOutcome Outcome, string? ExistingName = null);

/// <summary>
/// Adding mods by drag and drop.
/// </summary>
public sealed class ModImport(ModLibrary library)
{
    /// <summary>
    /// Copies a file into the mods folder if it is safe to do so.
    /// </summary>
    /// <param name="replace">
    /// Whether to replace a same-named mod with different contents. Never done
    /// without the player's consent: it may be a new version or a different mod entirely.
    /// </param>
    /// <exception cref="ModException">The file could not be read or written.</exception>
    public ModImportResult Import(string sourcePath, bool replace = false)
    {
        var name = Path.GetFileName(sourcePath);

        if (!File.Exists(sourcePath) || !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return new(name, ModImportOutcome.NotAssembly);

        if (!name.StartsWith(ModOverlay.RequiredPrefix, StringComparison.Ordinal))
            return new(name, ModImportOutcome.BadName);

        name = ExistingName(name) ?? name;

        var target = Path.Combine(library.Directory, name);

        try
        {
            var hash = Hash(sourcePath);

            if (File.Exists(target))
            {
                if (Hash(target).AsSpan().SequenceEqual(hash))
                    return new(name, ModImportOutcome.AlreadyPresent);

                if (!replace)
                    return new(name, ModImportOutcome.NeedsReplace);
            }
            else if (FindSameContent(hash, except: name) is { } existing)
            {
                return new(name, ModImportOutcome.DuplicateOf, existing);
            }

            var existed = File.Exists(target);
            CopyAtomically(sourcePath, target);

            if (!existed)
                library.SetEnabled(name, true);

            Log.Information("Mod {Name} {Action} via drag and drop", name, existed ? "replaced" : "added");

            return new(name, existed ? ModImportOutcome.Replaced : ModImportOutcome.Added);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error(e, "Failed to add mod {Name}", name);
            throw new ModException("error-mod-import-failed", ("file", name), ("reason", e.Message));
        }
    }

    private string? ExistingName(string name)
    {
        if (!Directory.Exists(library.Directory))
            return null;

        return Directory.EnumerateFiles(library.Directory, "*.dll")
            .Select(Path.GetFileName)
            .FirstOrDefault(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
    }

    private string? FindSameContent(byte[] hash, string except)
    {
        if (!Directory.Exists(library.Directory))
            return null;

        foreach (var path in Directory.EnumerateFiles(library.Directory, "*.dll"))
        {
            var name = Path.GetFileName(path);
            if (string.Equals(name, except, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Hash(path).AsSpan().SequenceEqual(hash))
                return name;
        }

        return null;
    }

    /// <summary>
    /// Copies to a temporary file alongside, then renames over the target.
    /// </summary>
    private static void CopyAtomically(string source, string target)
    {
        FileHelpers.EnsureDirectoryExists(Path.GetDirectoryName(target)!);

        var temp = target + ".importing";
        try
        {
            File.Copy(source, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static byte[] Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
