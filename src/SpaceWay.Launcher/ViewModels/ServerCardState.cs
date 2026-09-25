using System.Diagnostics;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// State shared by all server cards: which are expanded and what servers
/// reported about themselves.
/// </summary>
public sealed class ServerCardState(ServerInfoCache info)
{
    private readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);

    public ServerInfoCache Info => info;

    public bool IsExpanded(string address) => _expanded.Contains(ServerAddress.Normalize(address));

    public void SetExpanded(string address, bool expanded)
    {
        var key = ServerAddress.Normalize(address);

        if (expanded)
            _expanded.Add(key);
        else
            _expanded.Remove(key);
    }
}

/// <summary>A server link, shown as a button in the expanded card.</summary>
public sealed partial class ServerLinkViewModel(string name, Uri url, Geometry? icon)
{
    public string Name => name;

    /// <summary>Icon, or null if the server named an unknown one.</summary>
    public Geometry? Icon => icon;

    public bool HasIcon => icon != null;

    /// <summary>URL in the tooltip, so the destination is visible before clicking.</summary>
    public string Url => url.ToString();

    [RelayCommand]
    private void Open() => Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
}
