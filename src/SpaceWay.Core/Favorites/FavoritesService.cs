using SpaceWay.Core.Data;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Favorites;

/// <summary>
/// Favorites with an in-memory cache.
/// </summary>
public sealed class FavoritesService(FavoritesStore store)
{
    private Dictionary<string, FavoriteServer> _byAddress = [];

    /// <summary>Raised after any change to favorites.</summary>
    public event Action? Changed;

    public IReadOnlyList<FavoriteServer> Servers => [.. _byAddress.Values];

    public void Reload()
    {
        _byAddress = store.GetServers().ToDictionary(s => ServerAddress.Normalize(s.Address));
        Changed?.Invoke();
    }

    public bool IsFavorite(string address) =>
        _byAddress.ContainsKey(ServerAddress.Normalize(address));

    public FavoriteServer? Find(string address) =>
        _byAddress.GetValueOrDefault(ServerAddress.Normalize(address));

    /// <summary>
    /// Adds a server to favorites, or removes it if already there.
    /// </summary>
    /// <returns>State after toggling.</returns>
    public bool Toggle(string address, string? reportedName = null)
    {
        var key = ServerAddress.Normalize(address);

        if (_byAddress.TryGetValue(key, out var existing))
        {
            store.DeleteServer(existing.Id);
            _byAddress.Remove(key);
            Changed?.Invoke();
            return false;
        }

        var server = new FavoriteServer(Guid.NewGuid(), address)
        {
            ReportedName = reportedName,
            SortOrder = _byAddress.Count,
        };

        store.SaveServer(server);
        _byAddress[key] = server;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Adds a manually entered server: address and optionally a name.
    /// </summary>
    /// <returns>false if the server is already in favorites.</returns>
    public bool Add(string address, string? customName = null)
    {
        var key = ServerAddress.Normalize(address);

        if (_byAddress.ContainsKey(key))
            return false;

        var name = customName?.Trim();

        var server = new FavoriteServer(Guid.NewGuid(), address.Trim())
        {
            CustomName = string.IsNullOrEmpty(name) ? null : name,
            SortOrder = _byAddress.Count,
        };

        store.SaveServer(server);
        _byAddress[key] = server;
        Changed?.Invoke();
        return true;
    }

    public void Save(FavoriteServer server)
    {
        store.SaveServer(server);
        _byAddress[ServerAddress.Normalize(server.Address)] = server;
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        var entry = _byAddress.FirstOrDefault(p => p.Value.Id == id);
        if (entry.Key == null)
            return;

        store.DeleteServer(id);
        _byAddress.Remove(entry.Key);
        Changed?.Invoke();
    }

}
