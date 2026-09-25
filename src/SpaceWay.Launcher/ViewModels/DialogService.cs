using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Shows modal dialogs over the main window.
/// </summary>
public sealed partial class DialogService : ObservableObject
{
    [ObservableProperty]
    private IDialog? _current;

    public bool IsOpen => Current != null;

    /// <summary>Heading of the current dialog, kept up to date when its language changes.</summary>
    public string Title => Current?.Title ?? string.Empty;

    /// <summary>
    /// Whether the current dialog closes on backdrop click.
    /// </summary>
    public bool CanDismiss => Current?.DismissOnBackgroundClick ?? false;

    /// <summary>
    /// Shows a dialog and waits for it to close.
    /// </summary>
    public async Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        var underneath = Current;
        Current = dialog;

        try
        {
            return await dialog.Completion;
        }
        finally
        {
            if (ReferenceEquals(Current, dialog))
                Current = underneath;
        }
    }

    /// <summary>Closes the current dialog, on Escape or backdrop click.</summary>
    [RelayCommand]
    public void CloseCurrent() => Current?.Cancel();

    partial void OnCurrentChanging(IDialog? value)
    {
        if (Current is INotifyPropertyChanged old)
            old.PropertyChanged -= OnDialogPropertyChanged;
    }

    private void OnDialogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IDialog.Title) or null)
            OnPropertyChanged(nameof(Title));
    }

    partial void OnCurrentChanged(IDialog? value)
    {
        if (value is INotifyPropertyChanged current)
            current.PropertyChanged += OnDialogPropertyChanged;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(CanDismiss));
    }
}

/// <summary>Non-generic dialog view for the host.</summary>
public interface IDialog
{
    /// <summary>Heading shown by the host above the dialog content.</summary>
    string Title { get; }

    /// <summary>Close without a result: Escape, close button, backdrop click.</summary>
    void Cancel();

    /// <summary>Whether a click on the dimmed backdrop dismisses this dialog.</summary>
    bool DismissOnBackgroundClick => true;
}

public abstract class DialogViewModel<TResult> : LocalizedViewModel, IDialog
{
    private readonly TaskCompletionSource<TResult?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TResult?> Completion => _completion.Task;

    public abstract string Title { get; }

    public virtual bool DismissOnBackgroundClick => true;

    public virtual void Cancel() => _completion.TrySetResult(default);

    protected void Close(TResult result) => _completion.TrySetResult(result);

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(Title));
}
