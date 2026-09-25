namespace SpaceWay.Core.Updates;

/// <summary>A published launcher release.</summary>
/// <param name="Tag">Release tag as written on GitHub, with or without a leading <c>v</c>.</param>
/// <param name="Page">Release page with the changelog.</param>
/// <param name="DownloadBase">Base URL for files attached to the release, ending with <c>/</c>.</param>
public sealed record LauncherRelease(Version Version, string Tag, Uri Page, Uri DownloadBase)
{
    /// <summary>Checksum list published with every release, in <c>sha256sum</c> format.</summary>
    public const string ChecksumsFile = "SHA256SUMS";

    public string WindowsInstallerName => $"SpaceWayLauncher-{Version.ToString(3)}-setup.exe";

    public Uri FileUrl(string name) => new(DownloadBase, Uri.EscapeDataString(name));
}

/// <summary>Source of launcher releases.</summary>
public interface IReleaseSource
{
    /// <returns>null if there are no published releases.</returns>
    Task<LauncherRelease?> GetLatest(CancellationToken cancel = default);
}

/// <summary>
/// Latest release from GitHub. Uses the web redirect of <c>releases/latest</c> rather than
/// the REST API, which allows only 60 anonymous requests per hour per IP address.
/// Drafts and pre-releases are never returned by that redirect.
/// </summary>
public sealed class GitHubReleases(HttpClient http, string repository = GitHubReleases.DefaultRepository)
    : IReleaseSource
{
    public const string DefaultRepository = "NeBorsh/SpaceWayLauncher";

    private Uri RepositoryUrl => new($"https://github.com/{repository}/");

    public async Task<LauncherRelease?> GetLatest(CancellationToken cancel = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(RepositoryUrl, "releases/latest"));
        using var response = await http.SendAsync(request, cancel);

        response.EnsureSuccessStatusCode();

        return FromReleasePage(response.RequestMessage?.RequestUri);
    }

    /// <summary>Reads the release from the page <c>releases/latest</c> redirects to.</summary>
    /// <returns>null if the page is not a release, e.g. when nothing is published yet.</returns>
    public LauncherRelease? FromReleasePage(Uri? page)
    {
        if (page == null)
            return null;

        var prefix = new Uri(RepositoryUrl, "releases/tag/").AbsolutePath;
        var path = page.AbsolutePath;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var tag = Uri.UnescapeDataString(path[prefix.Length..].TrimEnd('/'));

        if (tag.Length == 0 || tag.Contains('/') || !LauncherVersion.TryParse(tag, out var version))
            return null;

        var downloads = new Uri(RepositoryUrl, $"releases/download/{Uri.EscapeDataString(tag)}/");

        return new LauncherRelease(version, tag, page, downloads);
    }

    /// <summary>Parses <c>sha256sum</c> output: a hex digest, whitespace, optional <c>*</c>, file name.</summary>
    /// <returns>File name to lowercase hex digest.</returns>
    public static IReadOnlyDictionary<string, string> ParseChecksums(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('﻿');
            var space = line.IndexOfAny([' ', '\t']);

            if (space != 64)
                continue;

            var hash = line[..space];
            var name = line[space..].TrimStart(' ', '\t', '*');

            if (name.Length == 0 || !hash.All(Uri.IsHexDigit))
                continue;

            result[name] = hash.ToLowerInvariant();
        }

        return result;
    }
}
