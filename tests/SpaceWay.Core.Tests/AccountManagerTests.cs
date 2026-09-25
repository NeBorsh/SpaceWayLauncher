using NUnit.Framework;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Data;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class AccountManagerTests
{
    private static readonly AuthServer ServerA =
        new(Guid.NewGuid(), "Официальный", new Uri("https://auth-a.example/"));

    private static readonly AuthServer ServerB =
        new(Guid.NewGuid(), "Свой", new Uri("https://auth-b.example/"));

    private LauncherDatabase _db = null!;
    private FakeAuthApi _api = null!;
    private FakeTokenStore _tokens = null!;
    private AccountManager _accounts = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        _api = new FakeAuthApi();
        _tokens = new FakeTokenStore();

        _accounts = new AccountManager(
            new AccountStore(_db), _tokens, _api, new SettingsStore(_db));

        _accounts.SaveServer(ServerA);
        _accounts.SaveServer(ServerB);
    }

    private void EnableOffline() => new AccountStore(_db).EnsureOfflineServer();

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task SuccessfulLogin_SavesAccountAndToken()
    {
        var result = await _accounts.LoginAsync(ServerA, "Игрок", "пароль");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_accounts.Accounts, Has.Count.EqualTo(1));
            Assert.That(_tokens.Get(_accounts.Accounts[0].SecretKey), Is.Not.Null);
        });
    }

    [Test]
    public async Task WrongPassword_CreatesNoAccount()
    {
        _api.Deny = AuthDenyReason.InvalidCredentials;

        var result = await _accounts.LoginAsync(ServerA, "Игрок", "неверный");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Reason, Is.EqualTo(AuthDenyReason.InvalidCredentials));
            Assert.That(_accounts.Accounts, Is.Empty);
        });
    }

    [Test]
    public async Task TwoFactorDemand_IsRecognized()
    {
        _api.Deny = AuthDenyReason.TfaRequired;

        var result = await _accounts.LoginAsync(ServerA, "Игрок", "пароль");

        Assert.That(result.NeedsTwoFactor, Is.True);
    }

    [Test]
    public async Task SameLoginOnDifferentServers_StaysTwoAccounts()
    {
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");
        await _accounts.LoginAsync(ServerB, "Игрок", "пароль");

        Assert.That(_accounts.Accounts, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Logout_RemovesTokenFromStore()
    {
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");
        var account = _accounts.Accounts[0];

        await _accounts.LogoutAsync(account);

        Assert.Multiple(() =>
        {
            Assert.That(_accounts.Accounts, Is.Empty);
            Assert.That(_tokens.Get(account.SecretKey), Is.Null);
            Assert.That(_api.LogoutCalled, Is.True, "the token must be revoked on the server");
        });
    }

    [Test]
    public async Task RemoveServer_ClearsTokensOfItsAccounts()
    {
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");
        var account = _accounts.Accounts[0];

        _accounts.RemoveServer(ServerA.Id);

        Assert.Multiple(() =>
        {
            Assert.That(_accounts.Accounts, Is.Empty);
            Assert.That(_tokens.Get(account.SecretKey), Is.Null);
        });
    }

    [Test]
    public void OfflineServerCannotBeRemoved()
    {
        EnableOffline();
        _accounts.Reload();

        Assert.Throws<InvalidOperationException>(
            () => _accounts.RemoveServer(AuthServer.OfflineId));

        Assert.That(_accounts.FindServer(AuthServer.OfflineId), Is.Not.Null);
    }

    [Test]
    public async Task RemoveServer_CountsWhatWillBeLost()
    {
        await _accounts.LoginAsync(ServerA, "Первый", "пароль");
        await _accounts.LoginAsync(ServerA, "Второй", "пароль");
        await _accounts.LoginAsync(ServerB, "Третий", "пароль");

        Assert.Multiple(() =>
        {
            Assert.That(_accounts.AccountsOn(ServerA.Id), Is.EqualTo(2));
            Assert.That(_accounts.AccountsOn(ServerB.Id), Is.EqualTo(1));
        });

        _accounts.RemoveServer(ServerA.Id);

        Assert.That(_accounts.AccountsOn(ServerB.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task FreshToken_IsNotRefreshed()
    {
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");

        var token = await _accounts.EnsureValidTokenAsync(_accounts.Accounts[0]);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Not.Null);
            Assert.That(_api.RefreshCalled, Is.False);
        });
    }

    [Test]
    public async Task TokenNearExpiry_IsRefreshed()
    {
        _api.TokenLifetime = TimeSpan.FromDays(3);
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");

        var token = await _accounts.EnsureValidTokenAsync(_accounts.Accounts[0]);

        Assert.Multiple(() =>
        {
            Assert.That(_api.RefreshCalled, Is.True);
            Assert.That(token!.Value, Is.EqualTo("продлённый"));
        });
    }

    [Test]
    public async Task ExpiredToken_DemandsNewLogin()
    {
        _api.TokenLifetime = TimeSpan.FromDays(-1);
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");

        var token = await _accounts.EnsureValidTokenAsync(_accounts.Accounts[0]);

        Assert.That(token, Is.Null);
    }

    [Test]
    public async Task ServerDownOnRefresh_KeepsOldToken()
    {
        _api.TokenLifetime = TimeSpan.FromDays(3);
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");
        _api.RefreshThrows = true;

        var token = await _accounts.EnsureValidTokenAsync(_accounts.Accounts[0]);

        Assert.That(token, Is.Not.Null);
    }

    [Test]
    public async Task AfterLogin_AccountBecomesSelected()
    {
        await _accounts.LoginAsync(ServerA, "Игрок", "пароль");

        Assert.That(_accounts.Selected?.Username, Is.EqualTo("Игрок"));
    }

    [Test]
    public async Task RemovingSelectedAccount_SelectsAnother()
    {
        await _accounts.LoginAsync(ServerA, "Первый", "пароль");
        await _accounts.LoginAsync(ServerB, "Второй", "пароль");

        await _accounts.LogoutAsync(_accounts.Selected!);

        Assert.That(_accounts.Selected, Is.Not.Null);
    }

    [Test]
    public void OfflineAccount_IsCreatedOnSeededDatabase()
    {
        EnableOffline();

        Assert.DoesNotThrow(() => _accounts.CreateOfflineAccount("Гость"));
        Assert.That(_accounts.Accounts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OfflineAccount_LivesWithoutToken()
    {
        EnableOffline();
        var account = _accounts.CreateOfflineAccount("Гость");

        var token = await _accounts.EnsureValidTokenAsync(account);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null, "an offline account never has a token");
            Assert.That(_api.RefreshCalled, Is.False, "nothing to refresh");
        });
    }

    [Test]
    public async Task OfflineLogout_DoesNotCallServer()
    {
        EnableOffline();
        var account = _accounts.CreateOfflineAccount("Гость");

        await _accounts.LogoutAsync(account);

        Assert.Multiple(() =>
        {
            Assert.That(_accounts.Accounts, Is.Empty);
            Assert.That(_api.LogoutCalled, Is.False, "nothing to revoke, there is no server");
        });
    }

    [Test]
    public void OfflineAccountsWithDifferentNames_Coexist()
    {
        EnableOffline();

        _accounts.CreateOfflineAccount("Первый");
        _accounts.CreateOfflineAccount("Второй");

        Assert.That(_accounts.Accounts, Has.Count.EqualTo(2));
    }
}
