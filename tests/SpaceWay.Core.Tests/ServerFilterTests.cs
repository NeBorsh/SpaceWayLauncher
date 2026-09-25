using NUnit.Framework;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ServerFilterTests
{
    private static readonly HubEntry Hub = new(
        Guid.NewGuid(), "Хаб", new Uri("https://hub.example/"), Priority: 0);

    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void SearchByName_IgnoresCase()
    {
        var servers = new[] { Server("Атмос Станция"), Server("Другой") };

        var result = new ServerFilter { Search = "атмос" }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("Атмос Станция"));
    }

    [Test]
    public void SearchByAddress_Works()
    {
        var servers = new[]
        {
            Server("Первый", address: "ss14://atmos.example:1212"),
            Server("Второй", address: "ss14://other.example:1212"),
        };

        var result = new ServerFilter { Search = "atmos" }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("Первый"));
    }

    [Test]
    public void HideFull_RemovesOnlyFullOnes()
    {
        var servers = new[]
        {
            Server("Полный", players: 50, maxPlayers: 50),
            Server("Свободный", players: 10, maxPlayers: 50),
        };

        var result = new ServerFilter { HideFull = true }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("Свободный"));
    }

    [Test]
    public void ServerWithoutLimit_IsNeverFull()
    {
        var servers = new[] { Server("Без лимита", players: 30, maxPlayers: 0) };

        var result = new ServerFilter { HideFull = true }.Apply(servers);

        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public void OverfilledServer_CountsAsFull()
    {
        var server = Server("Переполненный", players: 55, maxPlayers: 50);

        Assert.That(server.IsFull, Is.True);
    }

    [Test]
    public void LanguageFilter_LooksAtTags()
    {
        var servers = new[]
        {
            Server("Русский", tags: ["lang:ru"]),
            Server("Английский", tags: ["lang:en"]),
        };

        var result = new ServerFilter { Languages = ["ru"] }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("Русский"));
    }

    [Test]
    public void EmptyLanguageList_FiltersNothing()
    {
        var servers = new[] { Server("Русский", tags: ["lang:ru"]), Server("Без тега") };

        Assert.That(new ServerFilter().Apply(servers).Count(), Is.EqualTo(2));
    }

    [Test]
    public void RolePlayFilter_MatchesAnyListedLevel()
    {
        var servers = new[]
        {
            Server("Mixed", tags: ["rp:low", "rp:med", "rp:high"]),
            Server("Low", tags: ["rp:low"]),
            Server("Untagged"),
        };

        var result = new ServerFilter { RolePlayLevels = ["high"] }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("Mixed"));
    }

    [Test]
    public void TagGroups_CombineWithAnd()
    {
        var servers = new[]
        {
            Server("RuEast", tags: ["lang:ru", "region:eu_e"]),
            Server("RuWest", tags: ["lang:ru", "region:eu_w"]),
            Server("EnEast", tags: ["lang:en", "region:eu_e"]),
        };

        var result = new ServerFilter { Languages = ["ru"], Regions = ["eu_e"] }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("RuEast"));
    }

    [Test]
    public void SelectedValues_WithinGroup_CombineWithOr()
    {
        var servers = new[]
        {
            Server("Ru", tags: ["lang:ru"]),
            Server("En", tags: ["lang:en"]),
            Server("De", tags: ["lang:de"]),
        };

        var result = new ServerFilter { Languages = ["ru", "en"] }.Apply(servers).Select(s => s.DisplayName);

        Assert.That(result, Is.EquivalentTo(new[] { "Ru", "En" }));
    }

    [Test]
    public void TagAliases_AreResolved()
    {
        var server = Server("Aliased", tags: ["rp:MEDIUM", "rp:mrp", "rp:HRP", "region:us_e"]);

        Assert.Multiple(() =>
        {
            Assert.That(server.RolePlayLevels, Is.EquivalentTo(new[] { "med", "high" }));
            Assert.That(server.Regions, Is.EquivalentTo(new[] { "am_n_e" }));
        });
    }

    [Test]
    public void TagValues_IgnoreCase()
    {
        var servers = new[] { Server("Upper", tags: ["LANG:RU"]) };

        Assert.That(new ServerFilter { Languages = ["ru"] }.Apply(servers), Is.Not.Empty);
    }

    [Test]
    public void HideAdult_WorksByTag()
    {
        var servers = new[] { Server("Взрослый", tags: ["18+"]), Server("Обычный") };

        var result = new ServerFilter { HideAdultOnly = true }.Apply(servers);

        Assert.That(result.Single().DisplayName, Is.EqualTo("Обычный"));
    }

    [Test]
    public void SortByPlayers_GoesDescending()
    {
        var servers = new[] { Server("Мало", players: 3), Server("Много", players: 40) };

        var result = new ServerFilter { Sort = ServerSort.Players }.Apply(servers);

        Assert.That(result.First().DisplayName, Is.EqualTo("Много"));
    }

    [Test]
    public void SortByRoundTime_PutsLobbyLast()
    {
        var servers = new[]
        {
            Server("В лобби", roundStart: null),
            Server("Идёт час", roundStart: Now.AddHours(-1)),
            Server("Только начался", roundStart: Now.AddMinutes(-5)),
        };

        var result = new ServerFilter { Sort = ServerSort.RoundTime }.Apply(servers).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result[0].DisplayName, Is.EqualTo("Только начался"));
            Assert.That(result[^1].DisplayName, Is.EqualTo("В лобби"));
        });
    }

    [Test]
    public void SortByFullness_UsesTheLimit()
    {
        var servers = new[]
        {
            Server("Полупустой большой", players: 30, maxPlayers: 100),
            Server("Забитый маленький", players: 9, maxPlayers: 10),
        };

        var result = new ServerFilter { Sort = ServerSort.Occupancy }.Apply(servers);

        Assert.That(result.First().DisplayName, Is.EqualTo("Забитый маленький"));
    }

    [Test]
    public void RoundDuration_CountsFromStart()
    {
        var server = Server("Идёт", roundStart: Now.AddMinutes(-90));

        Assert.That(server.RoundDuration(Now), Is.EqualTo(TimeSpan.FromMinutes(90)));
    }

    [Test]
    public void StartTimeInFuture_GivesNoNegativeDuration()
    {
        var server = Server("Странный", roundStart: Now.AddMinutes(10));

        Assert.That(server.RoundDuration(Now), Is.Null);
    }

    private static MergedServer Server(
        string name,
        string? address = null,
        int players = 0,
        int maxPlayers = 50,
        string[]? tags = null,
        DateTimeOffset? roundStart = null)
    {
        return new MergedServer(
            Address: address ?? $"ss14://{Guid.NewGuid():N}.example:1212",
            NormalizedAddress: address ?? $"ss14://{Guid.NewGuid():N}.example:1212",
            Status: new ServerStatus
            {
                Name = name,
                Players = players,
                SoftMaxPlayers = maxPlayers,
                Tags = tags,
                RoundStartTime = roundStart,
            },
            InferredTags: [],
            Sources: [Hub]);
    }
}
