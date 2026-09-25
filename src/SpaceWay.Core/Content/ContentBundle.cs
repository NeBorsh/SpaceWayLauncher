using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace SpaceWay.Core.Content;

/// <summary>
/// Content bundle: the entire game in a single zip, no server needed.
/// SS14 replays are bundles too.
/// </summary>
public sealed class ContentBundle : IDisposable
{
    public const string MetadataFile = "rt_content_bundle.json";

    /// <summary>Fork ID under which bundles are stored in the content database.</summary>
    public const string ForkId = "AnonymousContentBundle";

    private readonly FileStream _file;

    private ContentBundle(string path, FileStream file, ZipArchive archive, ContentBundleMetadata metadata)
    {
        Path = path;
        _file = file;
        Archive = archive;
        Metadata = metadata;
    }

    public string Path { get; }

    public ZipArchive Archive { get; }

    public ContentBundleMetadata Metadata { get; }

    public bool HasResourceManifest => Archive.GetEntry("manifest.yml") != null;

    /// <summary>Run on top of the base build without touching the content database.</summary>
    public bool MountsAsOverlay => !HasResourceManifest && Metadata.BaseBuild != null;

    /// <exception cref="ContentUpdateException">The file is missing, not a zip, or not a bundle.</exception>
    public static ContentBundle Open(string path)
    {
        FileStream? file = null;
        ZipArchive? archive = null;

        try
        {
            file = File.OpenRead(path);
            archive = new ZipArchive(file, ZipArchiveMode.Read);

            var entry = archive.GetEntry(MetadataFile)
                        ?? throw new ContentUpdateException("error-not-a-bundle", null,
                            ("file", System.IO.Path.GetFileName(path)));

            ContentBundleMetadata? metadata;
            using (var stream = entry.Open())
                metadata = JsonSerializer.Deserialize<ContentBundleMetadata>(stream);

            if (metadata == null || string.IsNullOrEmpty(metadata.EngineVersion))
                throw new ContentUpdateException("error-bundle-bad-metadata");

            Log.Information(
                "Opened bundle {Path}: engine {Engine}, base build {Fork} {Version}",
                path, metadata.EngineVersion, metadata.BaseBuild?.ForkId, metadata.BaseBuild?.Version);

            return new ContentBundle(path, file, archive, metadata);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            archive?.Dispose();
            file?.Dispose();

            throw new ContentUpdateException("error-bundle-unreadable", e,
                ("file", System.IO.Path.GetFileName(path)));
        }
        catch (JsonException e)
        {
            archive?.Dispose();
            file?.Dispose();
            throw new ContentUpdateException("error-bundle-bad-metadata", e);
        }
        catch
        {
            archive?.Dispose();
            file?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Key that identifies the bundle in the content database.
    /// </summary>
    public byte[] ComputeKey()
    {
        _file.Seek(0, SeekOrigin.Begin);
        var zipHash = SHA256.HashData(_file);

        var salted = new byte[zipHash.Length * 2];
        zipHash.CopyTo(salted, 0);
        "content bundle change"u8.CopyTo(salted.AsSpan(zipHash.Length));

        return SHA256.HashData(salted);
    }

    public void Dispose()
    {
        Archive.Dispose();
        _file.Dispose();
    }
}

/// <summary>Contents of <c>rt_content_bundle.json</c>.</summary>
public sealed record ContentBundleMetadata(
    [property: JsonPropertyName("engine_version")] string EngineVersion,
    [property: JsonPropertyName("base_build")] ContentBundleBaseBuild? BaseBuild = null,
    [property: JsonPropertyName("server_gc")] bool? ServerGC = null)
{
    /// <summary>The base build as a server would report it.</summary>
    public ServerBuildInformation BaseBuildInformation()
    {
        if (BaseBuild is not { } build)
            throw new InvalidOperationException("Bundle has no base build");

        return new ServerBuildInformation
        {
            DownloadUrl = build.DownloadUrl,
            ManifestUrl = build.ManifestUrl,
            ManifestDownloadUrl = build.ManifestDownloadUrl,
            EngineVersion = EngineVersion,
            Version = build.Version,
            ForkId = build.ForkId,
            Hash = build.Hash,
            ManifestHash = build.ManifestHash,
        };
    }
}

/// <summary>Server build the bundle was recorded on.</summary>
public sealed record ContentBundleBaseBuild(
    [property: JsonPropertyName("fork_id")] string ForkId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("download_url")] string? DownloadUrl = null,
    [property: JsonPropertyName("hash")] string? Hash = null,
    [property: JsonPropertyName("manifest_download_url")] string? ManifestDownloadUrl = null,
    [property: JsonPropertyName("manifest_url")] string? ManifestUrl = null,
    [property: JsonPropertyName("manifest_hash")] string? ManifestHash = null);
