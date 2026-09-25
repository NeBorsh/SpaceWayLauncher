using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Mods;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Mods section: what is in the folder and what is enabled.
/// </summary>
public sealed partial class ModsViewModel : LocalizedViewModel
{
    private readonly ModLibrary _library;
    private readonly ModImport _import;
    private readonly DialogService _dialogs;

    [ObservableProperty]
    private string? _errorText;

    /// <summary>
    /// How long the drop result stays visible if everything went fine.
    /// </summary>
    private static readonly TimeSpan ImportTextLifetime = TimeSpan.FromSeconds(6);

    /// <summary>ID of the latest result, so an old timer does not hide a newer one.</summary>
    private int _importGeneration;

    /// <summary>Result of the latest drop, one line per file.</summary>
    [ObservableProperty]
    private string? _importText;

    /// <summary>Some dropped files were rejected: the result is highlighted and stays visible.</summary>
    [ObservableProperty]
    private bool _importHasProblems;

    public ModsViewModel(ModLibrary library, DialogService dialogs)
    {
        _library = library;
        _import = new ModImport(library);
        _dialogs = dialogs;
        _library.Changed += Reload;

        Reload();
    }

    public ObservableCollection<ModItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public string FolderPath => _library.Directory;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public bool HasImportText => !string.IsNullOrEmpty(ImportText);

    /// <summary>
    /// Number of mods sent to the game. Shown both here and in the connection
    /// window, so the use of mods is never hidden.
    /// </summary>
    public string EnabledText => Loc.T("mods-enabled-count", ("count", Items.Count(i => i.IsEnabled)));

    /// <summary>Opens the mods folder.</summary>
    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            Process.Start(new ProcessStartInfo(FolderPath) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to open mods folder");
            ErrorText = e.Message;
        }
    }

    /// <summary>
    /// Adds dropped files.
    /// </summary>
    public async Task ImportAsync(IReadOnlyList<string> paths)
    {
        ErrorText = null;
        var lines = new List<string>();
        var problems = false;

        foreach (var path in paths)
        {
            try
            {
                var result = _import.Import(path);

                if (result.Outcome == ModImportOutcome.NeedsReplace)
                    result = await AskReplace(path, result);

                lines.Add(Describe(result));
                problems |= IsProblem(result.Outcome);
            }
            catch (ModException e)
            {
                lines.Add(e.Message);
                problems = true;
            }
        }

        ShowImportText(string.Join(Environment.NewLine, lines), problems);
        Reload();
    }

    [RelayCommand]
    private void DismissImport() => ShowImportText(null, false);

    private void ShowImportText(string? text, bool problems)
    {
        var generation = ++_importGeneration;

        ImportText = text;
        ImportHasProblems = problems;

        if (text != null && !problems)
            _ = HideImportTextLater(generation);
    }

    private async Task HideImportTextLater(int generation)
    {
        await Task.Delay(ImportTextLifetime);

        if (generation == _importGeneration)
            ShowImportText(null, false);
    }

    /// <summary>
    /// A rejection worth reading. "Already exists" and a declined replacement
    /// are not rejections: nothing was lost, or the player chose so.
    /// </summary>
    private static bool IsProblem(ModImportOutcome outcome) => outcome is
        ModImportOutcome.DuplicateOf or ModImportOutcome.NotAssembly or ModImportOutcome.BadName;

    private async Task<ModImportResult> AskReplace(string path, ModImportResult result)
    {
        var replace = await _dialogs.ShowAsync(new ConfirmDialogViewModel(
            Loc.T("mods-replace-title"),
            Loc.T("mods-replace-text", ("file", result.FileName)),
            Loc.T("mods-replace-confirm")));

        return replace ? _import.Import(path, replace: true) : result;
    }

    private static string Describe(ModImportResult result) => result.Outcome switch
    {
        ModImportOutcome.Added => Loc.T("mods-import-added", ("file", result.FileName)),
        ModImportOutcome.Replaced => Loc.T("mods-import-replaced", ("file", result.FileName)),
        ModImportOutcome.AlreadyPresent => Loc.T("mods-import-already", ("file", result.FileName)),
        ModImportOutcome.DuplicateOf => Loc.T("mods-import-duplicate",
            ("file", result.FileName), ("existing", result.ExistingName ?? string.Empty)),

        ModImportOutcome.NeedsReplace => Loc.T("mods-import-skipped", ("file", result.FileName)),
        ModImportOutcome.BadName => Loc.T("mods-import-bad-name",
            ("file", result.FileName), ("prefix", ModOverlay.RequiredPrefix)),
        _ => Loc.T("mods-import-not-assembly", ("file", result.FileName)),
    };

    /// <summary>Rescans the folder, since files may have just been added.</summary>
    [RelayCommand]
    private void Refresh() => Reload();

    protected override void OnLanguageChanged() => Reload();

    private void Reload()
    {
        Items.Clear();

        foreach (var mod in _library.All())
            Items.Add(new ModItemViewModel(mod, _library));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EnabledText));
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnImportTextChanged(string? value) => OnPropertyChanged(nameof(HasImportText));
}

/// <summary>
/// A mod in the list.
/// </summary>
public sealed partial class ModItemViewModel(ModEntry mod, ModLibrary library) : LocalizedViewModel
{
    private bool _isEnabled = mod.Enabled;

    public string FileName => mod.FileName;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value))
                return;

            library.SetEnabled(mod.FileName, value);
        }
    }

    /// <summary>The file is missing or named so the engine will not load it.</summary>
    public bool HasProblem => !mod.IsUsable;

    public string StatusText
    {
        get
        {
            if (mod.Missing)
                return Loc.T("mods-missing");

            if (!mod.IsUsable)
                return Loc.T("mods-bad-name", ("prefix", ModOverlay.RequiredPrefix));

            return ByteFormat.Format(mod.Size);
        }
    }

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(StatusText));
}
