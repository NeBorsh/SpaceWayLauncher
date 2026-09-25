namespace SpaceWay.Core.Util;

/// <summary>
/// Links the launcher agrees to open in a browser.
/// </summary>
public static class WebLink
{
    public static bool TryParse(string? link, out Uri uri)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https")
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }
}
