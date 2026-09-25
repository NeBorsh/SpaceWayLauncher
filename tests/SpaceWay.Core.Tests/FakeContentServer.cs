using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;
using SpaceWay.Core.Content;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Fake game server speaking the incremental download protocol.
/// </summary>
public sealed class FakeContentServer : HttpMessageHandler
{
    public const string ManifestUrl = "https://server.example/manifest.txt";
    public const string DownloadUrl = "https://server.example/download";
    public const string ZipUrl = "https://server.example/content.zip";
    public const string EngineUrl = "https://builds.example/engine.zip";

    private readonly List<(string Path, byte[] Content)> _files = [];

    /// <summary>Whether to serve files precompressed; servers may do either.</summary>
    public bool PreCompress { get; set; }

    /// <summary>Protocol versions the server advertises in the OPTIONS response.</summary>
    public (int Min, int Max) Protocols { get; set; } = (1, 1);

    /// <summary>How many times files were requested and how many were sent.</summary>
    public int DownloadRequests { get; private set; }

    public int FilesSent { get; private set; }

    /// <summary>Cut the stream after this many files. Zero means never.</summary>
    public int BreakAfterFiles { get; set; }

    /// <summary>Tamper with file contents while keeping the manifest unchanged.</summary>
    public bool CorruptFiles { get; set; }

    /// <summary>Engine build served at <see cref="EngineUrl"/>.</summary>
    public byte[]? EnginePayload { get; set; }

    public void AddFile(string path, string content) =>
        _files.Add((path, Encoding.UTF8.GetBytes(content)));

    /// <summary>What the server reports about itself for manifest downloads.</summary>
    public ServerBuildInformation BuildInfoWithManifest(string engineVersion = "1.0.0") => new()
    {
        EngineVersion = engineVersion,
        Version = "1",
        ForkId = "тест",
        ManifestUrl = ManifestUrl,
        ManifestDownloadUrl = DownloadUrl,
        ManifestHash = Convert.ToHexString(ManifestHash()),
    };

    /// <summary>Same, but for legacy zip delivery.</summary>
    public ServerBuildInformation BuildInfoWithZip(string engineVersion = "1.0.0") => new()
    {
        EngineVersion = engineVersion,
        Version = "1",
        ForkId = "тест",
        DownloadUrl = ZipUrl,
        Hash = Convert.ToHexString(SHA256.HashData(BuildZip())),
    };

    /// <summary>Engine build manifest for the build served by this server.</summary>
    public EngineBuildManifest EngineManifest(Key signingKey, string version = "1.0.0")
    {
        var payload = EnginePayload ?? throw new InvalidOperationException("Engine build is not set");

        return new EngineBuildManifest(new Dictionary<string, EngineVersionInfo>
        {
            [version] = new(false, null, new Dictionary<string, EngineBuildInfo>
            {
                [RidSelector.Current] = new(
                    EngineUrl,
                    Convert.ToHexString(SHA256.HashData(payload)),
                    Convert.ToHexString(SignatureAlgorithm.Ed25519.Sign(signingKey, payload))),
            }),
        });
    }

    public byte[] ManifestHash() => ContentHash.OfStream(new MemoryStream(BuildManifest()));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        var url = request.RequestUri!.ToString();

        if (request.Method == HttpMethod.Options && url == DownloadUrl)
            return Task.FromResult(OptionsResponse());

        if (request.Method == HttpMethod.Get && url == ManifestUrl)
            return Task.FromResult(Ok(BuildManifest()));

        if (request.Method == HttpMethod.Get && url == ZipUrl)
            return Task.FromResult(Ok(BuildZip()));

        if (request.Method == HttpMethod.Get && url == EngineUrl && EnginePayload != null)
            return Task.FromResult(Ok(EnginePayload));

        if (request.Method == HttpMethod.Post && url == DownloadUrl)
            return DownloadResponse(request, cancel);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private HttpResponseMessage OptionsResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Robust-Download-Min-Protocol", Protocols.Min.ToString());
        response.Headers.Add("X-Robust-Download-Max-Protocol", Protocols.Max.ToString());
        return response;
    }

    private async Task<HttpResponseMessage> DownloadResponse(HttpRequestMessage request, CancellationToken cancel)
    {
        DownloadRequests++;

        var body = await request.Content!.ReadAsByteArrayAsync(cancel);
        var indices = new int[body.Length / sizeof(int)];
        for (var i = 0; i < indices.Length; i++)
            indices[i] = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(i * sizeof(int)));

        var stream = new MemoryStream();

        var flags = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(flags, PreCompress ? 1 : 0);
        stream.Write(flags);

        var sorted = SortedFiles();
        var sent = 0;

        foreach (var index in indices)
        {
            if (BreakAfterFiles > 0 && sent == BreakAfterFiles)
                break;

            var content = sorted[index].Content;

            if (CorruptFiles)
                content = Encoding.UTF8.GetBytes(new string('!', content.Length));

            var header = new byte[PreCompress ? 8 : 4];
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), content.Length);

            if (PreCompress)
            {
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4, 4), 0);
            }

            stream.Write(header);
            stream.Write(content);
            sent++;
        }

        FilesSent += sent;
        stream.Position = 0;

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
    }

    /// <summary>
    /// Manifest as the engine produces it: a header, then
    /// "hash space path" lines sorted by path.
    /// </summary>
    private byte[] BuildManifest()
    {
        var text = new StringBuilder("Robust Content Manifest 1\n");

        foreach (var (path, content) in SortedFiles())
            text.Append($"{Convert.ToHexString(ContentHash.OfStream(new MemoryStream(content)))} {path}\n");

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private byte[] BuildZip()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in SortedFiles())
            {
                using var entry = archive.CreateEntry(path).Open();
                entry.Write(content);
            }
        }

        return memory.ToArray();
    }

    private List<(string Path, byte[] Content)> SortedFiles() =>
        _files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();

    private static HttpResponseMessage Ok(byte[] payload) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
}
