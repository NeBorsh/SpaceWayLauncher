using System.Text.Json.Serialization;

namespace SpaceWay.Core.Hubs;

/// <summary>
/// Game server status. Comes both from the hub as part of the list
/// and directly from the server's HTTP API.
/// Schema: https://docs.spacestation14.io/en/engine/http-api
/// </summary>
public sealed record ServerStatus
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("map")]
    public string? Map { get; init; }

    [JsonPropertyName("preset")]
    public string? Preset { get; init; }

    [JsonPropertyName("players")]
    public int Players { get; init; }

    [JsonPropertyName("soft_max_players")]
    public int SoftMaxPlayers { get; init; }

    [JsonPropertyName("round_id")]
    public int? RoundId { get; init; }

    [JsonPropertyName("run_level")]
    public GameRunLevel RunLevel { get; init; }

    [JsonPropertyName("round_start_time")]
    public DateTimeOffset? RoundStartTime { get; init; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; init; }

    /// <summary>The server is 18+.</summary>
    [JsonPropertyName("baby_jail")]
    public bool BabyJail { get; init; }

    [JsonPropertyName("panic_bunker")]
    public bool PanicBunker { get; init; }

    /// <summary>Join queue length. Absent if there is no queue.</summary>
    [JsonPropertyName("queue")]
    public int? Queue { get; init; }
}

public enum GameRunLevel
{
    PreRoundLobby = 0,
    InRound = 1,
    PostRound = 2,
}
