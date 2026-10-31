using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;
using SpaceWay.Launcher.ViewModels;

namespace SpaceWay.Launcher.Tests;

[TestFixture]
public sealed class ServerMapTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t ")]
    public void MissingMapIsHidden(string? map)
    {
        using var db = LauncherDatabase.CreateInMemory();
        using var card = Card(db, map);

        Assert.That(card.HasMap, Is.False);
        Assert.That(card.MapText, Is.Empty);
    }

    [Test]
    public void MapLabelFollowsLanguageChangesAndPreservesServerReportedName()
    {
        using var db = LauncherDatabase.CreateInMemory();
        using var card = Card(db, " Snowball Station ");
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        try
        {
            Loc.SetLanguage("en-US");
            Assert.That(card.MapText, Is.EqualTo("Map: Snowball Station"));

            Loc.SetLanguage("ru-RU");
            Assert.That(card.MapText, Is.EqualTo("Карта: Snowball Station"));
            Assert.That(changed, Does.Contain(nameof(ServerItemViewModel.MapText)));
        }
        finally
        {
            Loc.SetLanguage(Loc.DefaultLanguage);
        }
    }

    private static ServerItemViewModel Card(LauncherDatabase db, string? map) => new(
        new MergedServer("ss14://game.example", "ss14://game.example:1212",
            new ServerStatus { Map = map }, [], []),
        new FavoritesService(new FavoritesStore(db)));
}
