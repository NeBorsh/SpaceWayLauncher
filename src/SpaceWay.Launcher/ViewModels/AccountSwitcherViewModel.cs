using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>Account picker at the bottom of the sidebar.</summary>
public sealed partial class AccountSwitcherViewModel : LocalizedViewModel
{
    private readonly AccountManager _accounts;
    private readonly Func<Task<bool>> _signIn;
    private bool _reloading;

    [ObservableProperty] private AccountChoice? _selected;

    public AccountSwitcherViewModel(AccountManager accounts, Func<Task<bool>> signIn)
    {
        _accounts = accounts;
        _signIn = signIn;
        _accounts.Changed += Reload;

        Reload();
    }

    public ObservableCollection<AccountChoice> Choices { get; } = [];

    public bool HasAccounts => Choices.Count > 0;

    [RelayCommand]
    private Task SignIn() => _signIn();

    partial void OnSelectedChanged(AccountChoice? value)
    {
        if (!_reloading && value != null && value.Account.SecretKey != _accounts.Selected?.SecretKey)
            _accounts.Select(value.Account);
    }

    protected override void OnLanguageChanged() => Reload();

    private void Reload()
    {
        _reloading = true;

        try
        {
            Choices.Clear();

            foreach (var account in _accounts.Accounts)
            {
                var server = _accounts.FindServer(account.AuthServerId);
                var serverName = server switch
                {
                    null => Loc.T("accounts-unknown-server"),
                    { IsOffline: true } => Loc.T("accounts-offline-badge"),
                    _ => server.DisplayName,
                };

                Choices.Add(new AccountChoice(account, serverName));
            }

            var selectedKey = _accounts.Selected?.SecretKey;
            Selected = Choices.FirstOrDefault(c => c.Account.SecretKey == selectedKey);
        }
        finally
        {
            _reloading = false;
        }

        OnPropertyChanged(nameof(HasAccounts));
    }
}

public sealed record AccountChoice(Account Account, string ServerName)
{
    public string Username => Account.Username;
}
