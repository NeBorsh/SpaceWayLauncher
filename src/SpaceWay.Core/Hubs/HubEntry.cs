namespace SpaceWay.Core.Hubs;

/// <summary>
/// A hub is a source of the server list. The list is editable; the official
/// hub is only the default.
/// </summary>
/// <param name="Priority">
/// When deduplicating servers from different hubs, the hub with the
/// lower value wins.
/// </param>
public sealed record HubEntry(
    Guid Id,
    string DisplayName,
    Uri Address,
    int Priority,
    bool Enabled = true)
{
    public static HubEntry Official { get; } = new(
        new Guid("00000000-0000-0000-0000-000000000001"),
        "Space Station 14",
        new Uri("https://hub.spacestation14.com/"),
        Priority: 0);
}
