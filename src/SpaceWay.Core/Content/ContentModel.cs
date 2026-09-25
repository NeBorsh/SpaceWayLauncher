using System.Text.Json.Serialization;
using SpaceWay.Core.Localization;

namespace SpaceWay.Core.Content;

/// <summary>Blob compression in the content database. Values are persisted and must not change.</summary>
public enum ContentCompression
{
    None = 0,
    Deflate = 1,
    ZStd = 2,
}

/// <summary>A content version in the database.</summary>
public sealed record ContentVersionRow(
    long Id,
    byte[] Hash,
    string? ForkId,
    string? ForkVersion,
    string LastUsed,
    byte[]? ZipHash);

/// <summary>
/// What the server reports about its build.
/// </summary>
public sealed record ServerBuildInformation
{
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; init; }

    [JsonPropertyName("manifest_url")]
    public string? ManifestUrl { get; init; }

    [JsonPropertyName("manifest_download_url")]
    public string? ManifestDownloadUrl { get; init; }

    [JsonPropertyName("engine_version")]
    public required string EngineVersion { get; init; }

    /// <summary>Content version as the server names it.</summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>Which codebase this is: the official build, a fork, or a custom build.</summary>
    [JsonPropertyName("fork_id")]
    public required string ForkId { get; init; }

    /// <summary>SHA-256 of the zip archive, for legacy delivery.</summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }

    /// <summary>Blake2b of the manifest, for manifest delivery.</summary>
    [JsonPropertyName("manifest_hash")]
    public string? ManifestHash { get; init; }

    /// <summary>
    /// The server built the content from its own sources and hosts it itself.
    /// </summary>
    [JsonPropertyName("acz")]
    public bool Acz { get; init; }

    /// <summary>Whether the server supports manifest-based incremental downloads.</summary>
    public bool SupportsManifest =>
        !string.IsNullOrEmpty(ManifestUrl)
        && !string.IsNullOrEmpty(ManifestDownloadUrl)
        && !string.IsNullOrEmpty(ManifestHash);
}

/// <summary>Everything needed about the content to launch the game.</summary>
/// <param name="VersionId">Row in <c>ContentVersion</c> the game reads files from.</param>
/// <param name="Modules">Engine dependencies: <c>Robust</c> and modules.</param>
public sealed record ContentLaunchInfo(
    long VersionId,
    (string Name, string Version)[] Modules);

/// <summary>Content update failed for a reason that can be shown to the player.</summary>
public sealed class ContentUpdateException(
    string key,
    Exception? inner = null,
    params (string Name, object Value)[] args)
    : LocalizedException(key, inner, args);
