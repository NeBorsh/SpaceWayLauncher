namespace SpaceWay.Core.Hubs;

/// <summary>Server list sort order.</summary>
public enum ServerSort
{
    /// <summary>By player count, descending.</summary>
    Players,

    /// <summary>By name.</summary>
    Name,

    /// <summary>By round duration, newest rounds first.</summary>
    RoundTime,

    /// <summary>By fill ratio relative to the player cap.</summary>
    Occupancy,
}

/// <summary>
/// Server selection criteria. A plain stateless record, easy to persist
/// in settings and to test.
/// </summary>
public sealed record ServerFilter
{
    public static ServerFilter Default { get; } = new();

    /// <summary>Case-insensitive search by name and address.</summary>
    public string Search { get; init; } = string.Empty;

    /// <summary>Hide servers with no free slots.</summary>
    public bool HideFull { get; init; }

    /// <summary>Hide servers with no players.</summary>
    public bool HideEmpty { get; init; }

    /// <summary>Hide servers marked 18+.</summary>
    public bool HideAdultOnly { get; init; }

    /// <summary>
    /// Languages from tags like <c>lang:ru</c>. An empty set means no filtering.
    /// </summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    public ServerSort Sort { get; init; } = ServerSort.Players;

    public IEnumerable<MergedServer> Apply(IEnumerable<MergedServer> servers) => Order(servers.Where(Matches));

    /// <summary>
    /// Sorting only, no filtering.
    /// </summary>
    public IEnumerable<MergedServer> Order(IEnumerable<MergedServer> servers)
    {
        return Sort switch
        {
            ServerSort.Name => servers.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase),

            ServerSort.RoundTime => servers
                .OrderBy(s => s.Status.RoundStartTime == null)
                .ThenByDescending(s => s.Status.RoundStartTime),

            ServerSort.Occupancy => servers
                .OrderByDescending(s => s.Status.SoftMaxPlayers > 0
                    ? (double)s.Status.Players / s.Status.SoftMaxPlayers
                    : 0),

            _ => servers.OrderByDescending(s => s.Status.Players),
        };
    }

    /// <summary>Whether a server passes the filter. Order is not considered.</summary>
    public bool Matches(MergedServer server)
    {
        if (HideFull && server.IsFull)
            return false;

        if (HideEmpty && server.Status.Players == 0)
            return false;

        if (HideAdultOnly && server.IsAdultOnly)
            return false;

        if (Languages.Count > 0 && !Languages.Contains(server.Language ?? string.Empty))
            return false;

        if (Search.Length > 0)
        {
            var name = server.DisplayName;
            if (!name.Contains(Search, StringComparison.CurrentCultureIgnoreCase)
                && !server.Address.Contains(Search, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
