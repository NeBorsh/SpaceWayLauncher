using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Prompt to sign in while there are no accounts.
/// </summary>
public sealed partial class SignInPromptViewModel : DialogViewModel<bool>
{
    public override string Title => Loc.T("sign-in-prompt-title");

    [RelayCommand]
    private void SignIn() => Close(true);

    [RelayCommand]
    private void Later() => Close(false);
}
