using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Adding a custom auth server. Accounts on it are then added
/// through the regular new-account dialog.
/// </summary>
public sealed partial class AddAuthServerDialogViewModel(AccountManager accounts) : DialogViewModel<bool>
{
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string? _errorText;

    public override string Title => Loc.T("auth-server-add-title");

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>
    /// What is wrong with the address. An empty field is not flagged, since the player has not started typing.
    /// </summary>
    public string? AddressProblemText
    {
        get
        {
            var problem = AuthServer.ParseAddress(Address, out var uri);

            return problem switch
            {
                AuthServerAddressProblem.Empty => null,
                AuthServerAddressProblem.Invalid => Loc.T("auth-server-address-invalid"),
                AuthServerAddressProblem.Insecure => Loc.T("auth-server-address-insecure"),
                _ when IsKnown(uri!) => Loc.T("auth-server-duplicate"),
                _ => null,
            };
        }
    }

    public bool HasAddressProblem => AddressProblemText != null;

    public bool CanSubmit => !string.IsNullOrWhiteSpace(DisplayName)
                             && AuthServer.ParseAddress(Address, out var uri) == AuthServerAddressProblem.None
                             && !IsKnown(uri!);

    [RelayCommand]
    private void Submit()
    {
        if (!CanSubmit || AuthServer.ParseAddress(Address, out var uri) != AuthServerAddressProblem.None)
            return;

        try
        {
            accounts.SaveServer(new AuthServer(Guid.NewGuid(), DisplayName.Trim(), uri!));
            Close(true);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to save auth server");
            ErrorText = e.Message;
        }
    }

    private bool IsKnown(Uri uri) => accounts.AuthServers.Any(s => s.Address == uri);

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(AddressProblemText));
    }

    partial void OnDisplayNameChanged(string value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnAddressChanged(string value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(AddressProblemText));
        OnPropertyChanged(nameof(HasAddressProblem));
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
