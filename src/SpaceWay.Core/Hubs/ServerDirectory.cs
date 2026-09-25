namespace SpaceWay.Core.Hubs;

/// <summary>
/// The most recent server list received from hubs.
/// </summary>
public sealed class ServerDirectory(ServerListService service)
{
    private Dictionary<string, MergedServer> _byAddress = [];

    /// <summary>Raised after every successful refresh.</summary>
    public event Action? Updated;

    public IReadOnlyList<MergedServer> Servers { get; private set; } = [];

    public IReadOnlyList<HubFailure> Failures { get; private set; } = [];

    /// <summary>At least one refresh has completed.</summary>
    public bool HasData { get; private set; }

    public async Task RefreshAsync(IReadOnlyList<HubEntry> hubs, CancellationToken cancel = default)
    {
        var result = await service.FetchAsync(hubs, cancel);

        Servers = result.Servers;
        Failures = result.Failures;
        _byAddress = result.Servers.ToDictionary(s => s.NormalizedAddress);
        HasData = true;

        Updated?.Invoke();
    }

    /// <summary>
    /// Finds a server by address in any entry. Null means the server is not
    /// listed by the hubs: it is down or unlisted.
    /// </summary>
    public MergedServer? Find(string address) =>
        _byAddress.GetValueOrDefault(ServerAddress.Normalize(address));
}
