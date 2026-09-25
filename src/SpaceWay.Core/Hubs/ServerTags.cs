namespace SpaceWay.Core.Hubs;

/// <summary>
/// Parsing of standard server tags.
/// Schema: https://docs.spacestation14.io/en/engine/http-api
/// </summary>
public static class ServerTags
{
    public const string AdultOnly = "18+";

    private const string LanguagePrefix = "lang:";
    private const string RolePlayPrefix = "rp:";
    private const string RegionPrefix = "region:";

    private static readonly Dictionary<string, string> NoAliases = [];

    /// <summary>Common non-standard spellings of role-play levels.</summary>
    private static readonly Dictionary<string, string> RolePlayAliases = new()
    {
        ["medium"] = "med",
        ["mrp"] = "med",
        ["lrp"] = "low",
        ["hrp"] = "high",
        ["hard"] = "high",
    };

    /// <summary>Common non-standard spellings of regions.</summary>
    private static readonly Dictionary<string, string> RegionAliases = new()
    {
        ["us_e"] = "am_n_e",
        ["us_w"] = "am_n_w",
        ["us_c"] = "am_n_c",
    };

    /// <summary>Lowercase values of <c>lang:</c> tags.</summary>
    public static IReadOnlyList<string> Languages(IEnumerable<string> tags) =>
        Values(tags, LanguagePrefix, NoAliases);

    /// <summary>Values of <c>rp:</c> tags, with known aliases resolved. A server may list several levels.</summary>
    public static IReadOnlyList<string> RolePlayLevels(IEnumerable<string> tags) =>
        Values(tags, RolePlayPrefix, RolePlayAliases);

    /// <summary>Values of <c>region:</c> tags, with known aliases resolved.</summary>
    public static IReadOnlyList<string> Regions(IEnumerable<string> tags) =>
        Values(tags, RegionPrefix, RegionAliases);

    private static IReadOnlyList<string> Values(
        IEnumerable<string> tags, string prefix, Dictionary<string, string> aliases) =>
    [
        .. tags
            .Where(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && t.Length > prefix.Length)
            .Select(t => t[prefix.Length..].Trim().ToLowerInvariant())
            .Select(v => aliases.GetValueOrDefault(v, v))
            .Distinct(),
    ];
}
