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

    /// <summary>
    /// Rebuilds the list only when the accounts themselves changed. Selecting an account also
    /// raises <see cref="AccountManager.Changed"/>, and replacing the items in the middle of
    /// a selection would leave the dropdown empty.
    /// </summary>
    private void Reload()
    {
        _reloading = true;

        try
        {
            var choices = _accounts.Accounts
                .Select(account => new AccountChoice(account, ServerNameOf(account)))
                .ToList();

            if (!choices.SequenceEqual(Choices))
            {
                Choices.Clear();

                foreach (var choice in choices)
                    Choices.Add(choice);
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

    private string ServerNameOf(Account account) => _accounts.FindServer(account.AuthServerId) switch
    {
        null => Loc.T("accounts-unknown-server"),
        { IsOffline: true } => Loc.T("accounts-offline-badge"),
        var server => server.DisplayName,
    };
}

public sealed record AccountChoice(Account Account, string ServerName)
{
    public string Username => Account.Username;
}
