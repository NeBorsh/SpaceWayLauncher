using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Robust.LoaderApi;

namespace SpaceWay.Loader;

/// <summary>
/// Serves files to the engine from a zip archive.
/// </summary>
internal sealed class ZipFileApi(ZipArchive archive, string prefix = "") : IFileApi, IDisposable
{
    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        var entry = archive.GetEntry(prefix + path);
        if (entry == null)
        {
            stream = null;
            return false;
        }

        var memory = new MemoryStream();
        lock (archive)
        {
            using var source = entry.Open();
            source.CopyTo(memory);
        }

        memory.Position = 0;
        stream = memory;
        return true;
    }

    public IEnumerable<string> AllFiles => archive.Entries
        .Where(e => e.Name.Length > 0 && e.FullName.StartsWith(prefix, StringComparison.Ordinal))
        .Select(e => e.FullName[prefix.Length..]);

    public void Dispose() => archive.Dispose();
}
