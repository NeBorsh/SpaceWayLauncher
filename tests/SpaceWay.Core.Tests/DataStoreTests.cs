using NUnit.Framework;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Data;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Mods;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class DataStoreTests
{
    private LauncherDatabase _db = null!;

    [SetUp]
    public void SetUp() => _db = LauncherDatabase.CreateInMemory();

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void RepeatedMigration_BreaksNothing()
    {
        Assert.DoesNotThrow(() => Migrator.Migrate(_db.Connection));
    }

    [Test]
    public void Settings_SurviveRoundTrip()
    {
        var settings = new SettingsStore(_db);

        settings.Set(SettingKeys.Language, "ru-RU");
        settings.Set(SettingKeys.Language, "en-US");

        Assert.That(settings.Get(SettingKeys.Language), Is.EqualTo("en-US"));
    }

    [Test]
    public void BrokenSetting_FallsBackToDefault()
    {
        var settings = new SettingsStore(_db);
        settings.Set("count", "не число");

        Assert.That(settings.Get("count", 42), Is.EqualTo(42));
    }

    [Test]
    public void Hubs_AreStoredInPriorityOrder()
    {
        var store = new HubStore(_db);
        store.Save(new HubEntry(Guid.NewGuid(), "Второй", new Uri("https://b.example/"), Priority: 5));
        store.Save(new HubEntry(Guid.NewGuid(), "Первый", new Uri("https://a.example/"), Priority: 1));

        Assert.That(store.GetAll().Select(h => h.DisplayName), Is.EqualTo(new[] { "Первый", "Второй" }));
    }

    [Test]
    public void RemovedOfficialHub_DoesNotComeBack()
    {
        var store = new HubStore(_db);
        store.SeedIfEmpty();
        store.Delete(HubEntry.Official.Id);

        store.SeedIfEmpty();
        Assert.That(store.GetAll(), Is.Not.Empty);
        Assert.That(store.GetAll().Any(h => h.Id == HubEntry.Official.Id), Is.True,
            "seeding only applies to an empty list, and the list was empty");
    }

    [Test]
    public void AccountsFromDifferentAuthServers_ShareUserId()
    {
        var accounts = new AccountStore(_db);
        var serverA = new AuthServer(Guid.NewGuid(), "A", new Uri("https://a.example/"));
        var serverB = new AuthServer(Guid.NewGuid(), "B", new Uri("https://b.example/"));
        accounts.SaveAuthServer(serverA);
        accounts.SaveAuthServer(serverB);

        var sharedId = Guid.NewGuid();
        accounts.SaveAccount(new Account(sharedId, "Игрок A", serverA.Id, DateTimeOffset.UtcNow));
        accounts.SaveAccount(new Account(sharedId, "Игрок B", serverB.Id, DateTimeOffset.UtcNow));

        Assert.That(accounts.GetAccounts(), Has.Count.EqualTo(2));
    }

    [Test]
    public void DeletingAuthServer_TakesItsAccounts()
    {
        var accounts = new AccountStore(_db);
        var server = new AuthServer(Guid.NewGuid(), "A", new Uri("https://a.example/"));
        accounts.SaveAuthServer(server);
        accounts.SaveAccount(new Account(Guid.NewGuid(), "Игрок", server.Id, DateTimeOffset.UtcNow));

        accounts.DeleteAuthServer(server.Id);

        Assert.That(accounts.GetAccounts(), Is.Empty);
    }

    [Test]
    public void TokenExpiry_SurvivesSaving()
    {
        var accounts = new AccountStore(_db);
        var server = AuthServer.Official;
        accounts.SaveAuthServer(server);

        var expiry = new DateTimeOffset(2026, 5, 1, 12, 30, 0, TimeSpan.FromHours(3));
        accounts.SaveAccount(new Account(Guid.NewGuid(), "Игрок", server.Id, expiry));

        Assert.That(accounts.GetAccounts()[0].TokenExpiry, Is.EqualTo(expiry));
    }

    [Test]
    public void FavoriteWithTags_IsSavedWhole()
    {
        var favorites = new FavoritesStore(_db);
        var server = new FavoriteServer(Guid.NewGuid(), "ss14://example.com:1212")
        {
            CustomName = "Мой сервер",
            Note = "тут играют атмосы",
            Tags = ["атмос", "рп"],
        };

        favorites.SaveServer(server);
        var loaded = favorites.GetServers().Single();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.CustomName, Is.EqualTo("Мой сервер"));
            Assert.That(loaded.Note, Is.EqualTo("тут играют атмосы"));
            Assert.That(loaded.Tags, Is.EquivalentTo(new[] { "атмос", "рп" }));
        });
    }

    [Test]
    public void SavingTwice_DoesNotDuplicateTags()
    {
        var favorites = new FavoritesStore(_db);
        var server = new FavoriteServer(Guid.NewGuid(), "ss14://example.com:1212")
        {
            Tags = ["атмос"],
        };

        favorites.SaveServer(server);
        favorites.SaveServer(server with { Tags = ["рп"] });

        Assert.That(favorites.GetServers().Single().Tags, Is.EqualTo(new[] { "рп" }));
    }

    [Test]
    public void LookupByAddress_IgnoresSpelling()
    {
        var favorites = new FavoritesStore(_db);
        favorites.SaveServer(new FavoriteServer(Guid.NewGuid(), "ss14://example.com:1212/"));

        Assert.That(favorites.FindByAddress("ss14://example.com"), Is.Not.Null);
    }

    [Test]
    public void EnabledMods_KeepOrderAcrossRestart()
    {
        var mods = new ModStore(_db);

        mods.Enable("Content.Base.dll");
        mods.Enable("Content.Overlay.dll");

        Assert.That(new ModStore(_db).GetEnabled(),
            Is.EqualTo(new[] { "Content.Base.dll", "Content.Overlay.dll" }));
    }

    [Test]
    public void EnablingTwice_DoesNotDuplicate()
    {
        var mods = new ModStore(_db);

        mods.Enable("Content.Base.dll");
        mods.Enable("Content.Base.dll");

        Assert.That(mods.GetEnabled(), Has.Count.EqualTo(1));
    }
}
