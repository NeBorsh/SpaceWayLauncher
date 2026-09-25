using System.IO.Compression;
using Serilog;

namespace SpaceWay.Core.Util;

public static class FileHelpers
{
    public static void EnsureDirectoryExists(string path) => Directory.CreateDirectory(path);

    /// <summary>Empties a directory without deleting it.</summary>
    public static void ClearDirectory(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
            return;

        foreach (var file in dir.EnumerateFiles())
            file.Delete();

        foreach (var child in dir.EnumerateDirectories())
            child.Delete(recursive: true);
    }

    public static void ExtractZipToDirectory(string directory, Stream zipStream)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        archive.ExtractToDirectory(directory, overwriteFiles: true);
    }

    /// <summary>
    /// Sets the executable bit on a file.
    /// </summary>
    public static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(
            path,
            File.GetUnixFileMode(path)
            | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    /// <summary>
    /// Asks NTFS to compress the directory contents.
    /// </summary>
    public static void MarkDirectoryCompressed(string path)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            NtfsCompression.Enable(path);
        }
        catch (Exception e)
        {
            Log.Debug(e, "Failed to enable compression for {Path}", path);
        }
    }

    /// <summary>
    /// Path to a temporary file that is deleted together with this object.
    /// </summary>
    public sealed class TempPath : IDisposable
    {
        public TempPath()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"spaceway-{Guid.NewGuid():N}.tmp");
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                File.Delete(Path);
            }
            catch (IOException e)
            {
                Log.Debug(e, "Failed to delete temporary file {Path}", Path);
            }
        }
    }
}
