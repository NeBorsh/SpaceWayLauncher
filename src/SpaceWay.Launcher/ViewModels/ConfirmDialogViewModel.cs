using CommunityToolkit.Mvvm.Input;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// "Are you sure?" prompt before an irreversible action.
/// </summary>
public sealed partial class ConfirmDialogViewModel(
    string title,
    string text,
    string confirmLabel) : DialogViewModel<bool>
{
    public override string Title => title;

    public string Text => text;

    public string ConfirmLabel => confirmLabel;

    [RelayCommand]
    private void Confirm() => Close(true);

    [RelayCommand]
    private void Decline() => Close(false);
}
