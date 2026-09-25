using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Util;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Launcher settings.
/// </summary>
public sealed partial class SettingsViewModel : LocalizedViewModel
{
    private readonly SettingsStore _settings;
    private readonly ContentUpdater _content;
    private readonly DialogService _dialogs;

    [ObservableProperty] private LanguageInfo _selectedLanguage;
    [ObservableProperty] private bool _compatEnabled;
    [ObservableProperty] private string _storageText = string.Empty;
    [ObservableProperty] private string _contentSizeText = string.Empty;
    [ObservableProperty] private string _engineSizeText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorText;

    public SettingsViewModel(
        SettingsStore settings,
        ContentUpdater content,
        DialogService dialogs)
    {
        _settings = settings;
        _content = content;
        _dialogs = dialogs;

        _selectedLanguage = Loc.Available.FirstOrDefault(l => l.Code == Loc.Current)
                            ?? Loc.Available[0];
        _compatEnabled = settings.GetBool(SettingKeys.DisplayCompat, false);

        _ = RefreshStorage();
    }

    public IReadOnlyList<LanguageInfo> Languages => Loc.Available;

    public string UserDataPath => LauncherPaths.DirUserData;

    public string LocalDataPath => LauncherPaths.DirLocalData;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>Opens the settings and accounts directory.</summary>
    [RelayCommand]
    private void OpenUserData() => OpenFolder(UserDataPath);

    /// <summary>Opens the downloads directory: content, engines, logs.</summary>
    [RelayCommand]
    private void OpenLocalData() => OpenFolder(LocalDataPath);

    /// <summary>Deletes server content.</summary>
    [RelayCommand]
    private Task ClearContentAsync() => ClearAsync(
        "settings-clear-content-title",
        "settings-clear-content-text",
        r => r.ContentBytes,
        _content.ClearContent);

    /// <summary>Deletes downloaded engine builds.</summary>
    [RelayCommand]
    private Task ClearEnginesAsync() => ClearAsync(
        "settings-clear-engines-title",
        "settings-clear-engines-text",
        r => r.EngineBytes,
        _content.ClearEngines);

    /// <summary>
    /// Asks for confirmation and cleans up.
    /// </summary>
    private async Task ClearAsync(
        string titleKey,
        string textKey,
        Func<StorageReport, long> size,
        Action clear)
    {
        var report = await Task.Run(LauncherStorage.Measure);

        var confirmed = await _dialogs.ShowAsync(new ConfirmDialogViewModel(
            Loc.T(titleKey),
            Loc.T(textKey, ("size", ByteFormat.Format(size(report)))),
            Loc.T("settings-clear-confirm")));

        if (!confirmed)
            return;

        IsBusy = true;
        ErrorText = null;

        try
        {
            await Task.Run(clear);
        }
        catch (ContentUpdateException e)
        {
            ErrorText = e.Message;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to clear downloads");
            ErrorText = e.Message;
        }
        finally
        {
            IsBusy = false;
            await RefreshStorage();
        }
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Languages));
        _ = RefreshStorage();
    }

    /// <summary>
    /// Recomputes used disk space.
    /// </summary>
    private async Task RefreshStorage()
    {
        var report = await Task.Run(LauncherStorage.Measure);

        StorageText = ByteFormat.Format(report.Total);
        ContentSizeText = ByteFormat.Format(report.ContentBytes);
        EngineSizeText = ByteFormat.Format(report.EngineBytes);
    }

    /// <summary>
    /// Shows a directory in the system file manager.
    /// </summary>
    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to open directory {Path}", path);
            ErrorText = e.Message;
        }
    }

    partial void OnSelectedLanguageChanged(LanguageInfo value)
    {
        Loc.SetLanguage(value.Code);
        _settings.Set(SettingKeys.Language, value.Code);
    }

    partial void OnCompatEnabledChanged(bool value) =>
        _settings.SetBool(SettingKeys.DisplayCompat, value);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
