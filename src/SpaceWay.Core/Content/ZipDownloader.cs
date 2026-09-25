using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Serilog;
using SpaceWay.Core.Util;
using SpaceWay.Vendor.ZStd;

namespace SpaceWay.Core.Content;

/// <summary>
/// Content delivery as a single zip archive.
/// </summary>
public sealed class ZipDownloader(HttpClient http)
{
    /// <inheritdoc cref="ManifestDownloader.Download"/>
    public async Task<byte[]> Download(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        using var temp = new FileHelpers.TempPath();

        var zipHash = await DownloadArchive(buildInfo, temp.Path, progress, cancel);
        ContentStore.SetZipHash(con, versionId, zipHash);

        progress?.Report(new ContentProgress(ContentStage.StoringFiles));

        await using var file = File.OpenRead(temp.Path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);

        Ingest(con, versionId, archive, progress, cancel);

        return ContentStore.ComputeManifestHash(con, versionId);
    }

    /// <returns>SHA-256 of the archive.</returns>
    private async Task<byte[]> DownloadArchive(
        ServerBuildInformation buildInfo,
        string path,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        Log.Information("Downloading content archive from {Url}", buildInfo.DownloadUrl);

        try
        {
            await using var file = File.Create(path, 4096, FileOptions.Asynchronous);

            await http.DownloadToStream(
                buildInfo.DownloadUrl!,
                file,
                (done, total) => progress?.Report(
                    new ContentProgress(ContentStage.DownloadingZip, done, total, InBytes: true)),
                cancel);
        }
        catch (Exception e) when (e is HttpRequestException or IOException
                                      && !cancel.IsCancellationRequested)
        {
            throw new ContentUpdateException("error-zip-fetch-failed", e,
                ("address", buildInfo.DownloadUrl ?? string.Empty));
        }

        await using var written = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(written, cancel);

        if (buildInfo.Hash is { } expected
            && !string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ContentUpdateException("error-zip-hash");
        }

        return hash;
    }

    /// <summary>Splits the archive into blobs and fills in the version manifest.</summary>
    /// <param name="overwrite">
    /// Whether to replace paths already in the version. Needed for bundles: they are
    /// laid over the base build and their files must override files with the same name.
    /// </param>
    internal static void Ingest(
        SqliteConnection con,
        long versionId,
        ZipArchive archive,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel,
        bool overwrite = false)
    {
        using var findContent = con.CreateCommand();
        findContent.CommandText = "SELECT Id FROM Content WHERE Hash = $hash";
        var pFindHash = findContent.Parameters.Add("$hash", SqliteType.Blob);
        findContent.Prepare();

        using var insertContent = con.CreateCommand();
        insertContent.CommandText = """
            INSERT INTO Content (Hash, Size, Compression, Data)
            VALUES ($hash, $size, $compression, zeroblob($dataSize))
            RETURNING Id
            """;
        var pHash = insertContent.Parameters.Add("$hash", SqliteType.Blob);
        var pSize = insertContent.Parameters.Add("$size", SqliteType.Integer);
        var pCompression = insertContent.Parameters.Add("$compression", SqliteType.Integer);
        var pDataSize = insertContent.Parameters.Add("$dataSize", SqliteType.Integer);
        insertContent.Prepare();

        using var insertManifest = con.CreateCommand();
        insertManifest.CommandText = overwrite
            ? "INSERT OR REPLACE INTO ContentManifest (VersionId, Path, ContentId) VALUES ($versionId, $path, $contentId)"
            : "INSERT INTO ContentManifest (VersionId, Path, ContentId) VALUES ($versionId, $path, $contentId)";
        insertManifest.Parameters.AddWithValue("$versionId", versionId);
        var pPath = insertManifest.Parameters.Add("$path", SqliteType.Text);
        var pContentId = insertManifest.Parameters.Add("$contentId", SqliteType.Integer);
        insertManifest.Prepare();

        var compressed = new MemoryStream();
        var done = 0;
        var newFiles = 0;

        foreach (var entry in archive.Entries)
        {
            cancel.ThrowIfCancellationRequested();

            if (++done % 100 == 0)
                progress?.Report(new ContentProgress(ContentStage.StoringFiles, done, archive.Entries.Count));

            if (entry.Name.Length == 0)
                continue;

            byte[] hash;
            using (var content = entry.Open())
            {
                hash = ContentHash.OfStream(content);
            }

            pFindHash.Value = hash;
            var contentId = findContent.ExecuteScalar() as long?;

            if (contentId == null)
            {
                newFiles++;
                contentId = StoreBlob(entry, hash, compressed,
                    con, insertContent, pHash, pSize, pCompression, pDataSize);
            }

            pPath.Value = entry.FullName;
            pContentId.Value = contentId.Value;
            insertManifest.ExecuteNonQuery();
        }

        Log.Debug("New files added from archive: {Count}", newFiles);
    }

    private static long StoreBlob(
        ZipArchiveEntry entry,
        byte[] hash,
        MemoryStream compressed,
        SqliteConnection con,
        SqliteCommand insert,
        SqliteParameter pHash,
        SqliteParameter pSize,
        SqliteParameter pCompression,
        SqliteParameter pDataSize)
    {
        var worthCompressing = entry.Length - entry.CompressedLength > 10;

        pHash.Value = hash;
        pSize.Value = entry.Length;

        long contentId;

        if (worthCompressing)
        {
            compressed.SetLength(0);

            using (var compressor = new ZStdCompressStream(compressed, ownStream: false))
            using (var source = entry.Open())
            {
                source.CopyTo(compressor);
                compressor.FlushEnd();
            }

            pCompression.Value = (int)ContentCompression.ZStd;
            pDataSize.Value = compressed.Length;
            contentId = (long)insert.ExecuteScalar()!;

            using var blob = new SqliteBlob(con, "Content", "Data", contentId);
            blob.Write(compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        }
        else
        {
            pCompression.Value = (int)ContentCompression.None;
            pDataSize.Value = entry.Length;
            contentId = (long)insert.ExecuteScalar()!;

            using var blob = new SqliteBlob(con, "Content", "Data", contentId);
            using var source = entry.Open();
            source.CopyTo(blob);
        }

        return contentId;
    }
}
