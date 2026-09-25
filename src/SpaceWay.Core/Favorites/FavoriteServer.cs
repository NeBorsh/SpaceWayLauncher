namespace SpaceWay.Core.Favorites;

/// <summary>
/// A favorite server. The official launcher stores only name, address and
/// time added; this record also keeps a custom name and cached server details.
/// </summary>
public sealed record FavoriteServer(Guid Id, string Address)
{
    /// <summary>Name reported by the server. Shown unless the player set a custom one.</summary>
    public string? ReportedName { get; init; }

    /// <summary>Custom name. Overrides <see cref="ReportedName"/>.</summary>
    public string? CustomName { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public int SortOrder { get; init; }

    public string DisplayName => CustomName ?? ReportedName ?? Address;
}
