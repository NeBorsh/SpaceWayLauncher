using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceWay.Core.Hubs;

/// <summary>HTTP client for a hub.</summary>
public sealed class HubApi(HttpClient http) : IHubApi
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    public async Task<HubServerEntry[]> GetServers(Uri hubAddress, CancellationToken cancel)
    {
        var url = new Uri(hubAddress, "api/servers");

        return await http.GetFromJsonAsync<HubServerEntry[]>(url, JsonOptions, cancel)
               ?? throw new JsonException("Hub returned an empty server list");
    }

    /// <summary>A server entry as returned by the hub.</summary>
    public sealed record HubServerEntry
    {
        [JsonPropertyName("address")]
        public required string Address { get; init; }

        [JsonPropertyName("statusData")]
        public required ServerStatus StatusData { get; init; }

        /// <summary>
        /// Tags inferred by the hub itself, e.g. language from the server name.
        /// The official hub sets them; third-party hubs may not.
        /// </summary>
        [JsonPropertyName("inferredTags")]
        public string[]? InferredTags { get; init; }
    }
}
