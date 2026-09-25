using Serilog;

namespace SpaceWay.Core.Hubs;

/// <summary>
/// Builds the server list from all enabled hubs.
/// </summary>
public sealed class ServerListService(IHubApi api)
{
    /// <summary>
    /// Queries hubs in parallel and merges the results.
    /// A failing hub does not affect the others: it ends up in
    /// <see cref="ServerListResult.Failures"/> instead of failing the whole list.
    /// </summary>
    public async Task<ServerListResult> FetchAsync(
        IReadOnlyList<HubEntry> hubs,
        CancellationToken cancel = default)
    {
        var enabled = hubs.Where(h => h.Enabled).ToList();

        var tasks = enabled.ToDictionary(
            hub => hub,
            hub => FetchOneAsync(hub, cancel));

        await Task.WhenAll(tasks.Values);

        var merged = new Dictionary<string, MergedServer>();
        var failures = new List<HubFailure>();

        foreach (var (hub, task) in tasks.OrderBy(p => p.Key.Priority))
        {
            if (task.Result is not { } entries)
            {
                failures.Add(new HubFailure(hub, task.Exception?.GetBaseException()));
                continue;
            }

            foreach (var entry in entries)
            {
                var key = ServerAddress.Normalize(entry.Address);

                if (merged.TryGetValue(key, out var existing))
                {
                    merged[key] = existing with { Sources = [.. existing.Sources, hub] };
                    continue;
                }

                merged[key] = new MergedServer(
                    Address: entry.Address,
                    NormalizedAddress: key,
                    Status: entry.StatusData,
                    InferredTags: entry.InferredTags ?? [],
                    Sources: [hub]);
            }
        }

        return new ServerListResult([.. merged.Values], failures);
    }

    private async Task<HubApi.HubServerEntry[]?> FetchOneAsync(HubEntry hub, CancellationToken cancel)
    {
        try
        {
            var entries = await api.GetServers(hub.Address, cancel);
            Log.Debug("Hub {Hub} returned {Count} servers", hub.DisplayName, entries.Length);
            return entries;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log.Warning(e, "Hub {Hub} is unavailable", hub.DisplayName);
            return null;
        }
    }
}

/// <param name="Sources">
/// Hubs advertising this server, in priority order.
/// The first one's data is displayed.
/// </param>
public sealed record MergedServer(
    string Address,
    string NormalizedAddress,
    ServerStatus Status,
    IReadOnlyList<string> InferredTags,
    IReadOnlyList<HubEntry> Sources)
{
    public string DisplayName => Status.Name ?? Address;

    /// <summary>Server tags plus hub-inferred tags, without duplicates.</summary>
    public IReadOnlyList<string> AllTags { get; } =
        [.. (Status.Tags ?? []).Concat(InferredTags).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>No free slots left.</summary>
    public bool IsFull => Status.SoftMaxPlayers > 0 && Status.Players >= Status.SoftMaxPlayers;

    public bool IsAdultOnly => AllTags.Contains(ServerTags.AdultOnly, StringComparer.OrdinalIgnoreCase);

    public string? Language => ServerTags.Language(AllTags);

    public string? RolePlay => ServerTags.RolePlay(AllTags);

    /// <summary>
    /// How long the current round has been running. Null if the round has not
    /// started or the server did not report the start time.
    /// </summary>
    public TimeSpan? RoundDuration(DateTimeOffset now) =>
        Status.RoundStartTime is { } start && start <= now ? now - start : null;
}

public sealed record HubFailure(HubEntry Hub, Exception? Error);

public sealed record ServerListResult(
    IReadOnlyList<MergedServer> Servers,
    IReadOnlyList<HubFailure> Failures);
