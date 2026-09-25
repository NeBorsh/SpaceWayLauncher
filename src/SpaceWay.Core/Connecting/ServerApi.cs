using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using SpaceWay.Core.Content;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Core.Connecting;

public interface IServerApi
{
    /// <summary>Queries what the server reports about itself.</summary>
    Task<ServerInfo> GetInfo(Uri serverAddress, CancellationToken cancel = default);
}

/// <summary>HTTP client for a game server.</summary>
public sealed class ServerApi(HttpClient http) : IServerApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,

        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    public async Task<ServerInfo> GetInfo(Uri serverAddress, CancellationToken cancel = default)
    {
        var address = ServerAddress.InfoAddress(serverAddress);

        Log.Debug("Querying server info: {Address}", address);

        ServerInfo info;
        try
        {
            info = await http.GetFromJsonAsync<ServerInfo>(address, JsonOptions, cancel)
                   ?? throw new JsonException("Server returned an empty response");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException
                                      && !cancel.IsCancellationRequested)
        {
            throw new ConnectException("error-server-unreachable", e);
        }

        return info with { Build = InferDownloadUrls(info.Build, serverAddress) };
    }

    /// <summary>
    /// Fills in content download URLs if the server did not provide them.
    /// </summary>
    private static ServerBuildInformation? InferDownloadUrls(
        ServerBuildInformation? build,
        Uri serverAddress)
    {
        if (build == null)
            return null;

        var selfHosted = build.Acz || string.IsNullOrEmpty(build.DownloadUrl);
        if (!selfHosted)
            return build;

        var api = ServerAddress.ApiAddress(serverAddress);

        var inferred = build with
        {
            DownloadUrl = new Uri(api, "client.zip").ToString(),
        };

        if (!build.Acz)
            return inferred;

        return inferred with
        {
            ManifestUrl = new Uri(api, "manifest.txt").ToString(),
            ManifestDownloadUrl = new Uri(api, "download").ToString(),
        };
    }
}

/// <summary>Connection failed for a reason that can be shown to the player.</summary>
public sealed class ConnectException(
    string key,
    Exception? inner = null,
    params (string Name, object Value)[] args)
    : LocalizedException(key, inner, args);
