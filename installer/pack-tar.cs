using System.Formats.Tar;
using System.IO.Compression;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet run pack-tar.cs -- <source dir> <output .tar.gz> <root folder name>");
    return 1;
}

var (source, output, root) = (Path.GetFullPath(args[0]), Path.GetFullPath(args[1]), args[2]);

string[] executables = ["SpaceWay.Launcher", "SpaceWay.Loader"];

const UnixFileMode regular = UnixFileMode.UserRead | UnixFileMode.UserWrite
                             | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

const UnixFileMode executable = regular | UnixFileMode.UserExecute
                                | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

await using var file = File.Create(output);
await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
await using var tar = new TarWriter(gzip, TarEntryFormat.Pax);

foreach (var path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    var relative = Path.GetRelativePath(source, path).Replace('\\', '/');

    await using var data = File.OpenRead(path);

    var entry = new PaxTarEntry(TarEntryType.RegularFile, $"{root}/{relative}")
    {
        Mode = executables.Contains(relative) ? executable : regular,
        ModificationTime = File.GetLastWriteTimeUtc(path),
        DataStream = data,
    };

    await tar.WriteEntryAsync(entry);
}

return 0;
