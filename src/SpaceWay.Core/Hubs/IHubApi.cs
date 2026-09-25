namespace SpaceWay.Core.Hubs;

/// <summary>
/// Abstraction over a hub, so list merging logic can be tested
/// without the network.
/// </summary>
public interface IHubApi
{
    Task<HubApi.HubServerEntry[]> GetServers(Uri hubAddress, CancellationToken cancel);
}
