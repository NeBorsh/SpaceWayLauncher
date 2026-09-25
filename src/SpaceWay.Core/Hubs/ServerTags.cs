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

    public static string? Language(IEnumerable<string> tags) => Value(tags, LanguagePrefix);

    public static string? RolePlay(IEnumerable<string> tags) => Value(tags, RolePlayPrefix);

    public static string? Region(IEnumerable<string> tags) => Value(tags, RegionPrefix);

    private static string? Value(IEnumerable<string> tags, string prefix) => tags
        .FirstOrDefault(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
}
