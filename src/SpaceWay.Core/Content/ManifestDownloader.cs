using System.Buffers.Binary;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using NSec.Cryptography;
using Serilog;
using SpaceWay.Vendor.ZStd;
using SpaceWay.Core.Localization;

namespace SpaceWay.Core.Content;

/// <summary>
/// Incremental content download based on the server manifest.
/// </summary>
public sealed class ManifestDownloader(HttpClient http)
{
    /// <summary>Supported download protocol version.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Manifest header. Anything else is not recognized.</summary>
    private const string ManifestHeader = "Robust Content Manifest 1";

    /// <summary>
    /// How much compression must save for a file to be stored compressed.
    /// </summary>
    private const int CompressionSavingsThreshold = 10;

    /// <summary>
    /// Downloads missing files of a version and fills in its manifest.
    /// </summary>
    /// <param name="downloadedBlobs">
    /// Receives the IDs of downloaded blobs. If the download is interrupted,
    /// this list protects them from cleanup and the next attempt resumes
    /// where it stopped.
    /// </param>
    /// <returns>Manifest hash, which also identifies the version.</returns>
    public async Task<byte[]> Download(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        long versionId,
        List<long> downloadedBlobs,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        progress?.Report(new ContentProgress(ContentStage.FetchingManifest));

        var manifest = await FetchManifest(buildInfo, cancel);
        var missing = FindMissingFiles(con, manifest);

        if (missing.Count > 0)
        {
            Log.Debug(
                "Missing {Count} of {Total} files, downloading from {Url}",
                missing.Count, manifest.Entries.Count, buildInfo.ManifestDownloadUrl);

            await DownloadMissingFiles(buildInfo, con, manifest, missing, downloadedBlobs, progress, cancel);
        }
        else
        {
            Log.Debug("All {Total} files present, nothing to download", manifest.Entries.Count);
        }

        progress?.Report(new ContentProgress(ContentStage.StoringFiles));
        FillManifest(con, versionId, manifest);

        return manifest.Hash;
    }

    /// <summary>
    /// Fetches the manifest and verifies it is the expected one.
    /// </summary>
    private async Task<FetchedManifest> FetchManifest(ServerBuildInformation buildInfo, CancellationToken cancel)
    {
        Log.Debug("Fetching content manifest from {Url}", buildInfo.ManifestUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, buildInfo.ManifestUrl);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd", 1));

        using var response = await Send(request, HttpCompletionOption.ResponseHeadersRead,
            "error-manifest-fetch-failed", cancel);

        await using var body = Decompressed(response, await response.Content.ReadAsStreamAsync(cancel));

        IncrementalHash.Initialize(HashAlgorithm.Blake2b_256, out var state);
        await using var hashing = new HashingReadStream(body, state);

        using var reader = new StreamReader(hashing, Encoding.UTF8);

        if (await reader.ReadLineAsync(cancel) != ManifestHeader)
            throw new ContentUpdateException("error-manifest-format");

        var entries = new List<ManifestEntry>();

        while (await reader.ReadLineAsync(cancel) is { } line)
        {
            if (line.Length == 0)
                continue;

            var separator = line.IndexOf(' ');
            if (separator <= 0)
                throw new ContentUpdateException("error-manifest-line");

            entries.Add(new ManifestEntry(
                Convert.FromHexString(line.AsSpan(0, separator)),
                line[(separator + 1)..]));
        }

        var hash = hashing.Finish();

        if (!string.Equals(Convert.ToHexString(hash), buildInfo.ManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new ContentUpdateException("error-manifest-hash");

        Log.Debug("Manifest accepted: {Count} files", entries.Count);

        return new FetchedManifest(hash, entries);
    }

    /// <summary>
    /// Selects files not yet in the database.
    /// </summary>
    /// <returns>Manifest line indices, which is what the server works with.</returns>
    private static List<int> FindMissingFiles(SqliteConnection con, FetchedManifest manifest)
    {
        using var command = con.CreateCommand();
        command.CommandText = "SELECT 1 FROM Content WHERE Hash = $hash";
        var parameter = command.Parameters.Add("$hash", SqliteType.Blob);
        command.Prepare();

        var missing = new List<int>();

        var alreadyQueued = new HashSet<byte[]>(HashComparer.Instance);

        for (var i = 0; i < manifest.Entries.Count; i++)
        {
            var hash = manifest.Entries[i].Hash;

            if (!alreadyQueued.Add(hash))
                continue;

            parameter.Value = hash;
            if (command.ExecuteScalar() == null)
                missing.Add(i);
        }

        return missing;
    }

    /// <summary>Downloads missing files and stores them in the database.</summary>
    private async Task DownloadMissingFiles(
        ServerBuildInformation buildInfo,
        SqliteConnection con,
        FetchedManifest manifest,
        List<int> missing,
        List<long> downloadedBlobs,
        IProgress<ContentProgress>? progress,
        CancellationToken cancel)
    {
        await CheckProtocolSupported(buildInfo.ManifestDownloadUrl!, cancel);

        var body = new byte[missing.Count * sizeof(int)];
        for (var i = 0; i < missing.Count; i++)
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(i * sizeof(int)), missing[i]);

        using var request = new HttpRequestMessage(HttpMethod.Post, buildInfo.ManifestDownloadUrl)
        {
            Content = new ByteArrayContent(body)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
            },
        };

        request.Headers.Add(
            "X-Robust-Download-Protocol",
            ProtocolVersion.ToString(CultureInfo.InvariantCulture));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd", 1));

        using var response = await Send(request, HttpCompletionOption.ResponseHeadersRead,
            "error-files-fetch-failed", cancel);

        await using var stream = Decompressed(response, await response.Content.ReadAsStreamAsync(cancel));

        var flags = await ReadFlags(stream, cancel);

        var preCompressed = (flags & DownloadStreamFlags.PreCompressed) != 0;

        using var compressor = preCompressed ? null : new ZStdCCtx();
        using var decompressor = preCompressed ? new ZStdDCtx() : null;

        var headerSize = preCompressed ? 8 : 4;
        var header = new byte[headerSize];

        var readBuffer = new byte[64 * 1024];
        var compressBuffer = new byte[64 * 1024];

        using var insert = con.CreateCommand();
        insert.CommandText = """
            INSERT INTO Content (Hash, Size, Compression, Data)
            VALUES ($hash, $size, $compression, zeroblob($dataSize))
            RETURNING Id
            """;
        var pHash = insert.Parameters.Add("$hash", SqliteType.Blob);
        var pSize = insert.Parameters.Add("$size", SqliteType.Integer);
        var pCompression = insert.Parameters.Add("$compression", SqliteType.Integer);
        var pDataSize = insert.Parameters.Add("$dataSize", SqliteType.Integer);
        insert.Prepare();

        for (var i = 0; i < missing.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();

            progress?.Report(new ContentProgress(ContentStage.DownloadingFiles, i, missing.Count));

            var entry = manifest.Entries[missing[i]];

            await stream.ReadExactlyAsync(header.AsMemory(0, headerSize), cancel);
            var size = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));

            if (size < 0)
                throw new ContentUpdateException("error-file-negative-length");

            EnsureCapacity(ref readBuffer, size);
            var data = readBuffer.AsMemory(0, size);

            var compression = ContentCompression.None;
            var toStore = data;

            if (preCompressed)
            {
                var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));

                if (compressedSize > 0)
                {
                    EnsureCapacity(ref compressBuffer, compressedSize);
                    var compressed = compressBuffer.AsMemory(0, compressedSize);
                    await stream.ReadExactlyAsync(compressed, cancel);

                    if (decompressor!.Decompress(data.Span, compressed.Span) != size)
                        throw new ContentUpdateException("error-file-wrong-size");

                    compression = ContentCompression.ZStd;
                    toStore = compressed;
                }
                else
                {
                    await stream.ReadExactlyAsync(data, cancel);
                }
            }
            else
            {
                await stream.ReadExactlyAsync(data, cancel);
            }

            if (!HashAlgorithm.Blake2b_256.Verify(data.Span, entry.Hash))
                throw new ContentUpdateException("error-file-corrupt", null, ("path", entry.Path));

            if (!preCompressed)
            {
                EnsureCapacity(ref compressBuffer, ZStd.CompressBound(size));
                var compressedSize = compressor!.Compress(compressBuffer, data.Span);

                if (compressedSize + CompressionSavingsThreshold < size)
                {
                    compression = ContentCompression.ZStd;
                    toStore = compressBuffer.AsMemory(0, compressedSize);
                }
            }

            pHash.Value = entry.Hash;
            pSize.Value = size;
            pCompression.Value = (int)compression;
            pDataSize.Value = toStore.Length;

            var contentId = (long)insert.ExecuteScalar()!;

            using (var blob = new SqliteBlob(con, "Content", "Data", contentId))
            {
                blob.Write(toStore.Span);
            }

            downloadedBlobs.Add(contentId);
        }

        progress?.Report(new ContentProgress(ContentStage.DownloadingFiles, missing.Count, missing.Count));
    }

    /// <summary>
    /// Checks that the server speaks a supported protocol version.
    /// </summary>
    private async Task CheckProtocolSupported(string url, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, url);
        using var response = await Send(request, HttpCompletionOption.ResponseContentRead,
            "error-protocol-check-failed", cancel);

        if (!TryReadIntHeader(response, "X-Robust-Download-Min-Protocol", out var min)
            || !TryReadIntHeader(response, "X-Robust-Download-Max-Protocol", out var max))
        {
            throw new ContentUpdateException("error-protocol-unknown");
        }

        Log.Debug("Server supports download protocols {Min} to {Max}", min, max);

        if (min > ProtocolVersion || max < ProtocolVersion)
        {
            throw new ContentUpdateException("error-protocol-mismatch", null,
                ("min", min), ("max", max), ("ours", ProtocolVersion));
        }
    }

    /// <summary>Fills in the version's file list, linking paths to blobs.</summary>
    private static void FillManifest(SqliteConnection con, long versionId, FetchedManifest manifest)
    {
        using var find = con.CreateCommand();
        find.CommandText = "SELECT Id FROM Content WHERE Hash = $hash";
        var pFindHash = find.Parameters.Add("$hash", SqliteType.Blob);
        find.Prepare();

        using var insert = con.CreateCommand();
        insert.CommandText =
            "INSERT INTO ContentManifest (VersionId, Path, ContentId) VALUES ($versionId, $path, $contentId)";
        insert.Parameters.AddWithValue("$versionId", versionId);
        var pPath = insert.Parameters.Add("$path", SqliteType.Text);
        var pContentId = insert.Parameters.Add("$contentId", SqliteType.Integer);
        insert.Prepare();

        var known = new Dictionary<byte[], long>(HashComparer.Instance);

        foreach (var entry in manifest.Entries)
        {
            if (!known.TryGetValue(entry.Hash, out var contentId))
            {
                pFindHash.Value = entry.Hash;
                contentId = find.ExecuteScalar() as long?
                            ?? throw new ContentUpdateException("error-file-missing-after-download",
                                null, ("path", entry.Path));

                known[entry.Hash] = contentId;
            }

            pPath.Value = entry.Path;
            pContentId.Value = contentId;
            insert.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Server request with a readable error instead of a system one.
    /// </summary>
    private async Task<HttpResponseMessage> Send(
        HttpRequestMessage request,
        HttpCompletionOption completion,
        string failureKey,
        CancellationToken cancel)
    {
        try
        {
            var response = await http.SendAsync(request, completion, cancel);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (Exception e) when (e is HttpRequestException or IOException
                                      && !cancel.IsCancellationRequested)
        {
            throw new ContentUpdateException(
                failureKey, e, ("address", request.RequestUri?.ToString() ?? string.Empty));
        }
    }

    private static async Task<DownloadStreamFlags> ReadFlags(Stream stream, CancellationToken cancel)
    {
        var buffer = new byte[4];
        await stream.ReadExactlyAsync(buffer, cancel);
        return (DownloadStreamFlags)BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Decompresses the stream if the server compressed it as a whole.
    /// </summary>
    private static Stream Decompressed(HttpResponseMessage response, Stream stream)
    {
        if (!response.Content.Headers.ContentEncoding.Contains("zstd"))
            return stream;

        Log.Debug("Response is compressed as a whole");
        return new ZStdDecompressStream(stream);
    }

    private static bool TryReadIntHeader(HttpResponseMessage response, string name, out int value)
    {
        value = 0;

        return response.Headers.TryGetValues(name, out var values)
               && values.FirstOrDefault() is { } raw
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void EnsureCapacity(ref byte[] buffer, int needed)
    {
        if (buffer.Length >= needed)
            return;

        buffer = new byte[(int)BitOperations.RoundUpToPowerOf2((uint)needed)];
    }

    [Flags]
    private enum DownloadStreamFlags
    {
        None = 0,

        /// <summary>Files in the stream are already compressed by the server.</summary>
        PreCompressed = 1 << 0,
    }

    private sealed record FetchedManifest(byte[] Hash, List<ManifestEntry> Entries);

    private readonly record struct ManifestEntry(byte[] Hash, string Path);

    /// <summary>Compares hashes by content; arrays otherwise compare by reference.</summary>
    private sealed class HashComparer : IEqualityComparer<byte[]>
    {
        public static readonly HashComparer Instance = new();

        public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj) => BinaryPrimitives.ReadInt32LittleEndian(obj);
    }

    /// <summary>
    /// Stream that computes Blake2b of everything read through it.
    /// </summary>
    private sealed class HashingReadStream(Stream inner, IncrementalHash state) : Stream
    {
        private IncrementalHash _state = state;

        public byte[] Finish() => IncrementalHash.Finalize(ref _state);

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            if (read > 0)
                IncrementalHash.Update(ref _state, buffer[..read]);

            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            var read = await inner.ReadAsync(buffer, cancel);
            if (read > 0)
                IncrementalHash.Update(ref _state, buffer.Span[..read]);

            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancel) =>
            ReadAsync(buffer.AsMemory(offset, count), cancel).AsTask();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
