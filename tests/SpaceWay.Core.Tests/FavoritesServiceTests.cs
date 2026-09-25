using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Favorites;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class FavoritesServiceTests
{
    private LauncherDatabase _db = null!;
    private FavoritesService _favorites = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        _favorites = new FavoritesService(new FavoritesStore(_db));
        _favorites.Reload();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public void Toggle_AddsAndRemoves()
    {
        const string address = "ss14://example.com:1212";

        Assert.That(_favorites.Toggle(address, "Тест"), Is.True);
        Assert.That(_favorites.IsFavorite(address), Is.True);

        Assert.That(_favorites.Toggle(address), Is.False);
        Assert.That(_favorites.IsFavorite(address), Is.False);
    }

    [Test]
    public void Toggle_SurvivesRestart()
    {
        _favorites.Toggle("ss14://example.com:1212", "Тест");

        var reopened = new FavoritesService(new FavoritesStore(_db));
        reopened.Reload();

        Assert.That(reopened.IsFavorite("ss14://example.com:1212"), Is.True);
    }

    [Test]
    public void AddressFromAnotherHub_IsTheSameServer()
    {
        _favorites.Toggle("ss14://example.com:1212/", "Тест");

        Assert.That(_favorites.IsFavorite("ss14://example.com"), Is.True);
    }

    [Test]
    public void ServerName_IsRememberedOnAdd()
    {
        _favorites.Toggle("ss14://example.com:1212", "Атмос Станция");

        Assert.That(_favorites.Find("ss14://example.com:1212")?.ReportedName,
            Is.EqualTo("Атмос Станция"));
    }

    [Test]
    public void ChangingFavorites_RaisesEvent()
    {
        var raised = 0;
        _favorites.Changed += () => raised++;

        _favorites.Toggle("ss14://example.com:1212");
        _favorites.Toggle("ss14://example.com:1212");

        Assert.That(raised, Is.EqualTo(2));
    }

    [Test]
    public void Add_KeepsCustomName()
    {
        Assert.That(_favorites.Add("ss14://example.com:1212", "Моя локалка"), Is.True);

        var saved = _favorites.Find("ss14://example.com:1212");

        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.CustomName, Is.EqualTo("Моя локалка"));
        Assert.That(saved.DisplayName, Is.EqualTo("Моя локалка"));
    }

    [Test]
    public void Add_WithoutName_ShowsAddress()
    {
        _favorites.Add("ss14://example.com:1212", "   ");

        var saved = _favorites.Find("ss14://example.com:1212");

        Assert.That(saved!.CustomName, Is.Null);
        Assert.That(saved.DisplayName, Is.EqualTo("ss14://example.com:1212"));
    }

    [Test]
    public void Add_SameServerTwice_Refused()
    {
        Assert.That(_favorites.Add("ss14://example.com:1212"), Is.True);

        Assert.That(_favorites.Add("ss14://example.com/"), Is.False);
        Assert.That(_favorites.Servers, Has.Count.EqualTo(1));
    }

    [Test]
    public void Add_SurvivesRestart()
    {
        _favorites.Add("ss14://192.168.0.10:1212", "Локалка");

        var reopened = new FavoritesService(new FavoritesStore(_db));
        reopened.Reload();

        Assert.That(reopened.Find("ss14://192.168.0.10:1212")?.CustomName, Is.EqualTo("Локалка"));
    }
}
