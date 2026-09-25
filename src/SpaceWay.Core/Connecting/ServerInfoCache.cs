using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Connecting;

/// <summary>
/// Server <c>/info</c> responses cached for the launcher's lifetime.
/// </summary>
public sealed class ServerInfoCache(IServerApi api)
{
    private readonly Dictionary<string, Task<ServerInfo>> _requests = new(StringComparer.OrdinalIgnoreCase);

    /// <exception cref="ConnectException">The address could not be parsed or the server did not respond.</exception>
    public Task<ServerInfo> Get(string address)
    {
        var key = ServerAddress.Normalize(address);

        if (_requests.TryGetValue(key, out var cached))
            return cached;

        var request = Fetch(key, address);
        _requests[key] = request;

        if (request.IsFaulted)
            _requests.Remove(key);

        return request;
    }

    private async Task<ServerInfo> Fetch(string key, string address)
    {
        try
        {
            if (!ServerAddress.TryParse(address, out var uri))
                throw new ConnectException("error-bad-server-address", null, ("address", address));

            return await api.GetInfo(uri);
        }
        catch
        {
            _requests.Remove(key);
            throw;
        }
    }
}
