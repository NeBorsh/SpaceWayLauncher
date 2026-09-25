using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Connecting;

/// <summary>Command line of the launcher.</summary>
public static class LaunchArguments
{
    public const string ConnectOption = "--connect";

    /// <summary>
    /// Server to connect to: <c>--connect address</c>, passed by the game when a server asks
    /// it to reconnect, or a bare <c>ss14://</c> link opened from a browser or chat.
    /// </summary>
    public static Uri? ConnectTarget(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].Trim();

            if (arg == ConnectOption)
                return i + 1 < args.Count && ServerAddress.TryParse(args[i + 1], out var target) ? target : null;

            if (IsServerLink(arg) && ServerAddress.TryParse(arg, out var link))
                return link;
        }

        return null;
    }

    private static bool IsServerLink(string arg) =>
        arg.StartsWith($"{ServerAddress.SchemeInsecure}://", StringComparison.OrdinalIgnoreCase)
        || arg.StartsWith($"{ServerAddress.SchemeSecure}://", StringComparison.OrdinalIgnoreCase);
}
