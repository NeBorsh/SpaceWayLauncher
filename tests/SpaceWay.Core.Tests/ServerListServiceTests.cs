using NUnit.Framework;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ServerListServiceTests
{
    private static readonly HubEntry PrimaryHub = new(
        Guid.NewGuid(), "Главный", new Uri("https://hub-a.example/"), Priority: 0);

    private static readonly HubEntry SecondaryHub = new(
        Guid.NewGuid(), "Запасной", new Uri("https://hub-b.example/"), Priority: 1);

    [Test]
    public async Task ServersFromDifferentHubs_AreMerged()
    {
        var api = new FakeHubApi
        {
            [PrimaryHub] = [Entry("ss14://a.example:1212", "A")],
            [SecondaryHub] = [Entry("ss14://b.example:1212", "B")],
        };

        var result = await new ServerListService(api).FetchAsync([PrimaryHub, SecondaryHub]);

        Assert.That(result.Servers.Select(s => s.DisplayName), Is.EquivalentTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task SameServerFromTwoHubs_IsNotDuplicated()
    {
        var api = new FakeHubApi
        {
            [PrimaryHub] = [Entry("ss14://shared.example:1212/", "Из главного")],
            [SecondaryHub] = [Entry("ss14://shared.example:1212", "Из запасного")],
        };

        var result = await new ServerListService(api).FetchAsync([PrimaryHub, SecondaryHub]);

        Assert.That(result.Servers, Has.Count.EqualTo(1));

        var server = result.Servers[0];
        Assert.Multiple(() =>
        {
            Assert.That(server.DisplayName, Is.EqualTo("Из главного"));
            Assert.That(server.Sources, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task FailedHub_DoesNotBreakOthers()
    {
        var api = new FakeHubApi
        {
            [PrimaryHub] = null,
            [SecondaryHub] = [Entry("ss14://b.example:1212", "B")],
        };

        var result = await new ServerListService(api).FetchAsync([PrimaryHub, SecondaryHub]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Servers, Has.Count.EqualTo(1));
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0].Hub, Is.EqualTo(PrimaryHub));
        });
    }

    [Test]
    public async Task DisabledHub_IsNotQueried()
    {
        var api = new FakeHubApi
        {
            [PrimaryHub with { Enabled = false }] = [Entry("ss14://a.example:1212", "A")],
        };

        var result = await new ServerListService(api)
            .FetchAsync([PrimaryHub with { Enabled = false }]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Servers, Is.Empty);
            Assert.That(result.Failures, Is.Empty);
        });
    }

    [Test]
    public async Task ServerAndHubTags_AreCombined()
    {
        var entry = Entry("ss14://a.example:1212", "A") with
        {
            StatusData = new ServerStatus { Name = "A", Tags = ["rp:low"] },
            InferredTags = ["lang:ru", "rp:low"],
        };
        var api = new FakeHubApi { [PrimaryHub] = [entry] };

        var result = await new ServerListService(api).FetchAsync([PrimaryHub]);

        Assert.That(result.Servers[0].AllTags, Is.EquivalentTo(new[] { "rp:low", "lang:ru" }));
    }

    private static HubApi.HubServerEntry Entry(string address, string name) => new()
    {
        Address = address,
        StatusData = new ServerStatus { Name = name },
    };

    /// <summary>Stub hub. null instead of a list means "hub unavailable".</summary>
    private sealed class FakeHubApi : IHubApi
    {
        private readonly Dictionary<Uri, HubApi.HubServerEntry[]?> _responses = [];

        public HubApi.HubServerEntry[]? this[HubEntry hub]
        {
            set => _responses[hub.Address] = value;
        }

        public Task<HubApi.HubServerEntry[]> GetServers(Uri hubAddress, CancellationToken cancel)
        {
            var response = _responses.GetValueOrDefault(hubAddress);
            return response == null
                ? Task.FromException<HubApi.HubServerEntry[]>(new HttpRequestException("hub unavailable"))
                : Task.FromResult(response);
        }
    }
}
