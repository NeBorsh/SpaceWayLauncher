using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Refresh command and loading state shared by Servers and Favorites.
/// Both lists rebuild when the shared directory publishes an update.
/// </summary>
public sealed partial class ServerRefreshViewModel(ServerDirectory directory, HubManager hubs) : LocalizedViewModel
{
    /// <summary>A hub refresh is in progress, regardless of which tab started it.</summary>
    [ObservableProperty] private bool _isLoading;

    /// <summary>Number of unavailable hubs in the most recent refresh.</summary>
    [ObservableProperty] private int _failedHubCount;

    /// <summary>Localized failure summary, rebuilt when the interface language changes.</summary>
    public string FailuresText => Loc.T("servers-hub-failed", ("count", FailedHubCount));

    /// <summary>Whether both tabs should show the hub failure warning.</summary>
    public bool HasFailures => FailedHubCount > 0;

    /// <summary>
    /// Queries enabled hubs and updates the shared directory.
    /// Calls made during an active refresh are ignored, including direct calls
    /// outside the command. Failures leave the command available for retry.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            await directory.RefreshAsync(hubs.Enabled);
            FailedHubCount = directory.Failures.Count;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to fetch server list");
            FailedHubCount = hubs.Enabled.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnFailedHubCountChanged(int value)
    {
        OnPropertyChanged(nameof(FailuresText));
        OnPropertyChanged(nameof(HasFailures));
    }

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(FailuresText));
}
