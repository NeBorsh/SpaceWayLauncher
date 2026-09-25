using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace SpaceWay.Core.Engine;

/// <summary>
/// Engine build manifests from the Space Wizards CDN.
/// </summary>
public sealed class RobustBuildsApi(HttpClient http) : IEngineManifestSource
{
    private static readonly string[] BaseUrls =
    [
        "https://robust-builds.cdn.spacestation14.com/",
        "https://robust-builds.fallback.cdn.spacestation14.com/",
    ];

    /// <summary>
    /// How long a manifest is considered fresh.
    /// </summary>
    public static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _lock = new(1);

    private EngineBuildManifest? _cachedBuilds;
    private DateTimeOffset _cacheValidUntil;

    public async Task<EngineBuildManifest> GetBuilds(CancellationToken cancel = default)
    {
        await _lock.WaitAsync(cancel);
        try
        {
            if (_cachedBuilds != null && _cacheValidUntil > DateTimeOffset.UtcNow)
                return _cachedBuilds;

            Log.Debug("Refreshing engine build manifest");

            var versions = await GetJson<Dictionary<string, EngineVersionInfo>>("manifest.json", cancel);

            _cachedBuilds = new EngineBuildManifest(versions);
            _cacheValidUntil = DateTimeOffset.UtcNow + CacheTime;
            return _cachedBuilds;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Drops the cache because the requested version was not in it.</summary>
    public void InvalidateBuilds()
    {
        _cacheValidUntil = DateTimeOffset.MinValue;
    }

    public async Task<EngineModuleManifest> GetModules(CancellationToken cancel = default) =>
        await GetJson<EngineModuleManifest>("modules.json", cancel);

    private async Task<T> GetJson<T>(string path, CancellationToken cancel)
    {
        Exception? lastError = null;

        foreach (var baseUrl in BaseUrls)
        {
            var url = baseUrl + path;
            try
            {
                return await http.GetFromJsonAsync<T>(url, JsonOptions, cancel)
                       ?? throw new JsonException($"Empty response from{url}");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.Warning(e, "Failed to fetch {Url}, trying next URL", url);
                lastError = e;
            }
        }

        throw new EngineUpdateException(
            $"Failed to fetch {path} from any URL:{lastError?.Message}");
    }
}
