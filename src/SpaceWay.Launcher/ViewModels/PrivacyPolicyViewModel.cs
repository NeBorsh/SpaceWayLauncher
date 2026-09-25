using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Util;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Asks for consent to the server's privacy policy.
/// </summary>
public sealed partial class PrivacyPolicyViewModel(
    string serverName,
    ServerPrivacyPolicy policy,
    bool versionChanged)
    : DialogViewModel<bool>
{
    public override string Title => Loc.T("privacy-title");

    public string ServerName => serverName;

    public string Link => policy.Link;

    /// <summary>
    /// The player already accepted a different version.
    /// </summary>
    public bool VersionChanged => versionChanged;

    public string Explanation => Loc.T(
        versionChanged ? "privacy-changed" : "privacy-explanation",
        ("server", serverName));

    /// <summary>Closing by any other means counts as declining.</summary>
    public override void Cancel() => Close(false);

    [RelayCommand]
    private void Accept() => Close(true);

    [RelayCommand]
    private void Decline() => Close(false);

    [RelayCommand]
    private void OpenLink()
    {
        if (!WebLink.TryParse(policy.Link, out var uri))
        {
            Log.Warning("Server policy link rejected as unsafe: {Link}", policy.Link);
            return;
        }

        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }
}
