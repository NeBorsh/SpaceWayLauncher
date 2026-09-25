using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>Step of the add-account wizard.</summary>
public enum AddAccountStep
{
    /// <summary>Choosing a sign-in method.</summary>
    ChooseKind,

    /// <summary>Username and password on the auth server.</summary>
    Login,

    /// <summary>Name for playing without authentication.</summary>
    Offline,
}

public sealed partial class AddAccountDialogViewModel : DialogViewModel<bool>
{
    private readonly AccountManager _accounts;

    [ObservableProperty] private AddAccountStep _step = AddAccountStep.ChooseKind;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _twoFactorCode = string.Empty;
    [ObservableProperty] private bool _needsTwoFactor;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private AuthServer? _authServer;

    public AddAccountDialogViewModel(AccountManager accounts)
    {
        _accounts = accounts;
    }

    public bool IsChoosingKind => Step == AddAccountStep.ChooseKind;

    public bool IsLogin => Step == AddAccountStep.Login;

    public bool IsOffline => Step == AddAccountStep.Offline;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>
    /// Auth servers, excluding the pseudo-server for unauthenticated play.
    /// </summary>
    public IReadOnlyList<AuthServerChoice> KnownServers =>
        [.. _accounts.AuthServers
            .Where(s => !s.IsOffline)
            .Select(s => new AuthServerChoice(s, ChooseServer))];

    public override string Title => Step switch
    {
        AddAccountStep.Login => AuthServer?.DisplayName ?? Loc.T("add-account-title"),
        AddAccountStep.Offline => Loc.T("add-account-offline-title"),
        _ => Loc.T("add-account-title"),
    };

    public bool CanSubmitLogin => !IsBusy
                                  && !string.IsNullOrWhiteSpace(Username)
                                  && !string.IsNullOrEmpty(Password);

    public bool CanSubmitOffline => UsernameValidation.IsValid(Username);

    /// <summary>
    /// What is wrong with the name. An empty field is not flagged, since the player has not started typing.
    /// </summary>
    public string? UsernameProblemText => UsernameValidation.Check(Username) switch
    {
        UsernameProblem.None or UsernameProblem.Empty => null,
        UsernameProblem.TooShort => Loc.T("username-too-short",
            ("min", UsernameValidation.MinLength)),
        UsernameProblem.TooLong => Loc.T("username-too-long",
            ("max", UsernameValidation.MaxLength)),
        _ => Loc.T("username-invalid-characters"),
    };

    public bool HasUsernameProblem => UsernameProblemText != null;

    /// <summary>Sign-in on a known auth server.</summary>
    private void ChooseServer(AuthServer server)
    {
        AuthServer = server;
        ErrorText = null;
        Step = AddAccountStep.Login;
    }

    [RelayCommand]
    private void ChooseOffline()
    {
        ErrorText = null;
        Step = AddAccountStep.Offline;
    }

    [RelayCommand]
    private void Back()
    {
        ErrorText = null;
        NeedsTwoFactor = false;
        Password = string.Empty;
        Step = AddAccountStep.ChooseKind;
    }

    [RelayCommand]
    private async Task SubmitLoginAsync()
    {
        if (AuthServer is not { } server || !CanSubmitLogin)
            return;

        IsBusy = true;
        ErrorText = null;

        try
        {
            var result = await _accounts.LoginAsync(
                server,
                Username.Trim(),
                Password,
                NeedsTwoFactor ? TwoFactorCode.Trim() : null);

            if (result.IsSuccess)
            {
                Password = string.Empty;
                TwoFactorCode = string.Empty;
                Close(true);
                return;
            }

            NeedsTwoFactor = result.NeedsTwoFactor;
            ErrorText = DescribeFailure(result);
        }
        catch (Exception e)
        {
            Log.Error(e, "Sign-in failed");
            ErrorText = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SubmitOffline()
    {
        if (!CanSubmitOffline)
            return;

        try
        {
            _accounts.CreateOfflineAccount(Username);
            Close(true);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to create offline account");
            ErrorText = e.Message;
        }
    }

    private static string DescribeFailure(AuthResult result) => result.Reason switch
    {
        AuthDenyReason.InvalidCredentials => Loc.T("auth-error-credentials"),
        AuthDenyReason.AccountUnconfirmed => Loc.T("auth-error-unconfirmed"),
        AuthDenyReason.TfaRequired => Loc.T("auth-error-tfa-required"),
        AuthDenyReason.TfaInvalid => Loc.T("auth-error-tfa-invalid"),
        AuthDenyReason.AccountLocked => Loc.T("auth-error-locked"),
        AuthDenyReason.ConnectionError => Loc.T("auth-error-connection"),

        _ => result.Errors.Count > 0 ? string.Join("\n", result.Errors) : Loc.T("auth-error-unknown"),
    };

    partial void OnStepChanged(AddAccountStep value)
    {
        OnPropertyChanged(nameof(IsChoosingKind));
        OnPropertyChanged(nameof(IsLogin));
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(Title));
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSubmitLogin));

    partial void OnUsernameChanged(string value)
    {
        OnPropertyChanged(nameof(CanSubmitLogin));
        OnPropertyChanged(nameof(CanSubmitOffline));
        OnPropertyChanged(nameof(UsernameProblemText));
        OnPropertyChanged(nameof(HasUsernameProblem));
    }

    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(CanSubmitLogin));
}

/// <summary>Auth server list item with its own command.</summary>
public sealed partial class AuthServerChoice(AuthServer server, Action<AuthServer> choose)
    : ObservableObject
{
    public string DisplayName => server.DisplayName;

    public string Address => server.Address.AbsoluteUri;

    [RelayCommand]
    private void Choose() => choose(server);
}
