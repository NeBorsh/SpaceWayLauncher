using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Data;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Updates;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Launcher updates: the sidebar notice and the settings section share this state.
/// </summary>
public sealed partial class UpdatesViewModel : LocalizedViewModel
{
    private readonly UpdateService _updates;
    private readonly SettingsStore _settings;

    [ObservableProperty] private bool _checkEnabled;

    public UpdatesViewModel(UpdateService updates, SettingsStore settings)
    {
        _updates = updates;
        _settings = settings;
        _checkEnabled = settings.GetBool(SettingKeys.UpdatesCheck, true);

        _updates.Changed += () => Dispatcher.UIThread.Post(Refresh);
    }

    private UpdateState State => _updates.State;

    private string NewVersion => _updates.Latest?.Version.ToString(3) ?? string.Empty;

    public string CurrentVersionText =>
        Loc.T("settings-updates-current", ("version", LauncherVersion.CurrentText));

    public bool IsNoticeVisible => State is UpdateState.Available or UpdateState.Ready;

    /// <summary>Ready to install: closing the launcher runs the installer.</summary>
    public bool IsReady => State == UpdateState.Ready;

    /// <summary>A newer release exists, but this copy has to be updated by hand.</summary>
    public bool IsManual => State == UpdateState.Available;

    public bool IsBusy => State is UpdateState.Checking or UpdateState.Downloading;

    public string NoticeText => State == UpdateState.Ready
        ? Loc.T("update-ready", ("version", NewVersion))
        : Loc.T("update-available", ("version", NewVersion));

    public string? StatusText => State switch
    {
        UpdateState.Checking => Loc.T("update-status-checking"),
        UpdateState.UpToDate => Loc.T("update-status-up-to-date"),
        UpdateState.Available => Loc.T("update-available", ("version", NewVersion)),
        UpdateState.Downloading => Loc.T("update-status-downloading", ("version", NewVersion)),
        UpdateState.Ready => Loc.T("update-ready", ("version", NewVersion)),
        UpdateState.Failed => _updates.Error is LocalizedException e
            ? e.Message
            : Loc.T("update-status-failed"),
        _ => null,
    };

    public bool HasStatus => StatusText != null;

    public bool IsFailed => State == UpdateState.Failed;

    [RelayCommand]
    private Task CheckNow() => _updates.Check();

    /// <summary>Opens the release page: the changelog, and the downloads for manual updates.</summary>
    [RelayCommand]
    private void OpenRelease()
    {
        if (_updates.Latest?.Page is { } page)
            Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true });
    }

    partial void OnCheckEnabledChanged(bool value) => _settings.SetBool(SettingKeys.UpdatesCheck, value);

    protected override void OnLanguageChanged() => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(IsNoticeVisible));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsManual));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(NoticeText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(IsFailed));
    }
}
