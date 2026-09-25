using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

public sealed partial class AccountsViewModel : LocalizedViewModel
{
    private readonly AccountManager _accounts;
    private readonly DialogService _dialogs;

    public AccountsViewModel(AccountManager accounts, DialogService dialogs)
    {
        _accounts = accounts;
        _dialogs = dialogs;
        _accounts.Changed += Reload;

        Reload();
    }

    public ObservableCollection<AccountItemViewModel> Items { get; } = [];

    /// <summary>
    /// Auth servers, with removal.
    /// </summary>
    public ObservableCollection<AuthServerItemViewModel> Servers { get; } = [];

    public bool HasAccounts => Items.Count > 0;

    /// <summary>
    /// Warning shown only when tokens are actually stored in plain text;
    /// it never appears on Windows, macOS, or Linux with a keystore.
    /// </summary>
    public bool ShowInsecureWarning => !_accounts.TokensAreSecure;

    public string InsecureWarningText => Loc.T("accounts-insecure-storage");

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        await _dialogs.ShowAsync(new AddAccountDialogViewModel(_accounts));
    }

    [RelayCommand]
    private async Task AddServerAsync()
    {
        await _dialogs.ShowAsync(new AddAuthServerDialogViewModel(_accounts));
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(InsecureWarningText));
        Reload();
    }

    private void Reload()
    {
        var selected = _accounts.Selected;

        Items.Clear();
        foreach (var account in _accounts.Accounts)
        {
            var server = _accounts.FindServer(account.AuthServerId);
            Items.Add(new AccountItemViewModel(
                account,
                server?.DisplayName ?? Loc.T("accounts-unknown-server"),
                account.SecretKey == selected?.SecretKey,
                server?.IsOffline ?? false,
                _accounts));
        }

        Servers.Clear();
        foreach (var server in _accounts.AuthServers.Where(s => !s.IsOffline))
        {
            Servers.Add(new AuthServerItemViewModel(server, _accounts, _dialogs));
        }

        OnPropertyChanged(nameof(HasAccounts));
    }
}

/// <summary>
/// An auth server in the list.
/// </summary>
public sealed partial class AuthServerItemViewModel(
    AuthServer server,
    AccountManager accounts,
    DialogService dialogs) : LocalizedViewModel
{
    public string DisplayName => server.DisplayName;

    public string Address => server.Address.AbsoluteUri;

    /// <summary>
    /// Number of accounts removed along with the server. Always shown, so the
    /// cost of removal is visible before the player clicks.
    /// </summary>
    public string AccountsText =>
        Loc.T("accounts-server-accounts", ("count", accounts.AccountsOn(server.Id)));

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(AccountsText));

    /// <summary>
    /// Removes the server with its accounts and tokens.
    /// </summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        var confirmed = await dialogs.ShowAsync(new ConfirmDialogViewModel(
            Loc.T("accounts-server-remove-title"),
            Loc.T("accounts-server-remove-text",
                ("server", server.DisplayName), ("count", accounts.AccountsOn(server.Id))),
            Loc.T("accounts-server-remove-confirm")));

        if (confirmed)
            accounts.RemoveServer(server.Id);
    }
}

public sealed partial class AccountItemViewModel(
    Account account,
    string authServerName,
    bool isSelected,
    bool isOffline,
    AccountManager accounts) : LocalizedViewModel
{
    public string Username => account.Username;

    public string AuthServerName => authServerName;

    public bool IsSelected => isSelected;

    public bool IsOffline => isOffline;

    /// <summary>
    /// Token expiration. Lets the player know when the password
    /// will have to be entered again.
    /// </summary>
    public string ExpiryText
    {
        get
        {
            if (isOffline)
                return Loc.T("accounts-offline-badge");

            var left = account.TokenExpiry - DateTimeOffset.UtcNow;

            if (left <= TimeSpan.Zero)
                return Loc.T("accounts-token-expired");

            return Loc.T("accounts-token-valid", ("days", (int)left.TotalDays));
        }
    }

    public bool IsExpired => !isOffline && account.TokenExpiry <= DateTimeOffset.UtcNow;

    protected override void OnLanguageChanged() => OnPropertyChanged(nameof(ExpiryText));

    [RelayCommand]
    private void Select() => accounts.Select(account);

    [RelayCommand]
    private async Task LogoutAsync() => await accounts.LogoutAsync(account);
}
