using SpaceWay.Core.Data;

namespace SpaceWay.Core.Hubs;

/// <summary>
/// The hub list and its changes.
/// </summary>
public sealed class HubManager(HubStore store)
{
    private List<HubEntry> _hubs = [];

    public event Action? Changed;

    public IReadOnlyList<HubEntry> Hubs => _hubs;

    /// <summary>Enabled hubs only, in priority order; these are the ones queried.</summary>
    public IReadOnlyList<HubEntry> Enabled => [.. _hubs.Where(h => h.Enabled)];

    public void Reload()
    {
        _hubs = [.. store.GetAll()];
        Changed?.Invoke();
    }

    public void Save(HubEntry hub)
    {
        store.Save(hub);
        Reload();
    }

    public void Remove(Guid id)
    {
        store.Delete(id);
        Reload();
    }

    public void SetEnabled(Guid id, bool enabled)
    {
        if (_hubs.FirstOrDefault(h => h.Id == id) is not { } hub)
            return;

        Save(hub with { Enabled = enabled });
    }

    /// <summary>
    /// Moves a hub to the given position in the list.
    /// </summary>
    public void Reorder(Guid id, int targetIndex)
    {
        var ordered = _hubs.OrderBy(h => h.Priority).ToList();
        var index = ordered.FindIndex(h => h.Id == id);

        if (index < 0)
            return;

        targetIndex = Math.Clamp(targetIndex, 0, ordered.Count - 1);
        if (targetIndex == index)
            return;

        var moved = ordered[index];
        ordered.RemoveAt(index);
        ordered.Insert(targetIndex, moved);

        Renumber(ordered);
        Reload();
    }

    /// <summary>
    /// Moves a hub along the priority list.
    /// </summary>
    public void Move(Guid id, int offset)
    {
        var ordered = _hubs.OrderBy(h => h.Priority).ToList();
        var index = ordered.FindIndex(h => h.Id == id);

        if (index < 0)
            return;

        var target = index + offset;
        if (target < 0 || target >= ordered.Count)
            return;

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

        Renumber(ordered);
        Reload();
    }

    /// <summary>
    /// Renumbers priorities consecutively from zero.
    /// </summary>
    private void Renumber(List<HubEntry> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Priority != i)
                store.Save(ordered[i] with { Priority = i });
        }
    }
}
