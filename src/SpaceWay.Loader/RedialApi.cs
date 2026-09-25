using System.Diagnostics;
using Robust.LoaderApi;

namespace SpaceWay.Loader;

/// <summary>
/// Lets the game ask the launcher to reconnect it to a server.
/// </summary>
internal sealed class RedialApi(string launcherPath) : IRedialApi
{
    /// <summary>
    /// What to strip from the new launcher's environment.
    /// </summary>
    private static readonly string[] Inherited =
    [
        "ROBUST_AUTH_TOKEN",
        "ROBUST_AUTH_USERID",
        "ROBUST_AUTH_PUBKEY",
        "ROBUST_AUTH_SERVER",

        "SPACEWAY_CONTENT_DB",
        "SPACEWAY_CONTENT_VERSION",
        "SPACEWAY_OVERLAY_ZIP",
        "SPACEWAY_BUNDLE_ZIP",
        "DOTNET_gcServer",
        "SPACEWAY_LAUNCHER_PATH",
        "SPACEWAY_DISABLE_SIGNING",
    ];

    public void Redial(Uri uri, string text = "")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = launcherPath,
            UseShellExecute = false,
            ArgumentList = { "--connect", uri.ToString() },
        };

        if (!string.IsNullOrEmpty(text))
        {
            startInfo.ArgumentList.Add("--reason");
            startInfo.ArgumentList.Add(text);
        }

        foreach (var name in Inherited)
            startInfo.EnvironmentVariables.Remove(name);

        foreach (var name in startInfo.EnvironmentVariables.Keys.Cast<string>()
                     .Where(k => k.StartsWith("ROBUST_MODULE_", StringComparison.Ordinal))
                     .ToArray())
        {
            startInfo.EnvironmentVariables.Remove(name);
        }

        Process.Start(startInfo);
    }
}
