using NUnit.Framework;
using SpaceWay.Core.Data;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;
using SpaceWay.Launcher.ViewModels;

namespace SpaceWay.Launcher.Tests;

[TestFixture]
public sealed class ServerRefreshTests
{
    private const string Address = "ss14://game.example";
    private LauncherDatabase _db = null!;
    private StubHubApi _api = null!;
    private ServerRefreshViewModel _refresh = null!;
    private FavoritesService _favorites = null!;
    private ServersViewModel _servers = null!;
    private FavoritesViewModel _favoriteList = null!;

    [SetUp]
    public void SetUp()
    {
        _db = LauncherDatabase.CreateInMemory();
        var hubs = new HubManager(new HubStore(_db));
        hubs.Save(HubEntry.Official);
        _api = new StubHubApi();
        var directory = new ServerDirectory(new ServerListService(_api));
        _refresh = new ServerRefreshViewModel(directory, hubs);
        _favorites = new FavoritesService(new FavoritesStore(_db));
        _favorites.Add(Address, "My station");
        _servers = new ServersViewModel(directory, _refresh, new SettingsStore(_db), _favorites);
        _favoriteList = new FavoritesViewModel(_favorites, directory, _refresh, new DialogService());
    }

    [TearDown]
    public void TearDown()
    {
        _servers.Dispose();
        _favoriteList.Dispose();
        _refresh.Dispose();
        _db.Dispose();
    }

    [Test]
    public async Task RefreshFromEitherTabUpdatesBothListsAndPreservesFiltersAndFavorites()
    {
        await _servers.Refresh.RefreshCommand.ExecuteAsync(null);
        _servers.Search = "Station";
        _servers.LanguageTags.Chips.Single().IsSelected = true;
        var favoriteId = _favorites.Servers.Single().Id;

        _api.Respond = () => Task.FromResult(new[] { Entry("Snowball", 42) });
        await _favoriteList.Refresh.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_servers.Servers.Single().Server!.Status.Map, Is.EqualTo("Snowball"));
            Assert.That(_favoriteList.Items.Single().MapText, Is.EqualTo(_servers.Servers.Single().MapText));
            Assert.That(_favoriteList.Items.Single().Server!.Status.Players, Is.EqualTo(42));
            Assert.That(_favoriteList.Items.Single().Name, Is.EqualTo("My station"));
            Assert.That(_favorites.Servers.Single().Id, Is.EqualTo(favoriteId));
            Assert.That(_servers.Search, Is.EqualTo("Station"));
            Assert.That(_servers.LanguageTags.Selected, Is.EquivalentTo(new[] { "ru" }));
            Assert.That(_servers.ShownCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RefreshInProgressIsSharedAndDoesNotStartAnotherRequest()
    {
        var pending = new TaskCompletionSource<HubApi.HubServerEntry[]>();
        _api.Respond = () => pending.Task;
        var first = _favoriteList.Refresh.RefreshCommand.ExecuteAsync(null);

        try
        {
            await _servers.Refresh.RefreshAsync();

            Assert.Multiple(() =>
            {
                Assert.That(_api.Calls, Is.EqualTo(1));
                Assert.That(_servers.Refresh.IsLoading, Is.True);
                Assert.That(_servers.Refresh.RefreshCommand.CanExecute(null), Is.False);
                Assert.That(_servers.IsEmpty, Is.False);
                Assert.That(_favoriteList.IsEmpty, Is.False);
            });
        }
        finally
        {
            pending.SetResult([Entry("Box", 10)]);
            await first;
        }

        Assert.That(_refresh.IsLoading, Is.False);
        Assert.That(_refresh.RefreshCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task FailedHubIsVisibleInBothTabsAndCanBeRetried()
    {
        _api.Respond = () => throw new HttpRequestException("Unavailable");
        await _favoriteList.Refresh.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_servers.Refresh.HasFailures, Is.True);
            Assert.That(_favoriteList.Refresh.FailedHubCount, Is.EqualTo(1));
            Assert.That(_favoriteList.Items.Single().IsOffline, Is.True);
            Assert.That(_favoriteList.Items.Single().HasMap, Is.False);
            Assert.That(_refresh.IsLoading, Is.False);
        });

        _api.Respond = () => Task.FromResult(new[] { Entry("Box", 10) });
        await _favoriteList.Refresh.RefreshCommand.ExecuteAsync(null);

        Assert.That(_refresh.HasFailures, Is.False);
        Assert.That(_favoriteList.Items.Single().IsOffline, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CenteredLoadingIndicatorOnlyAppearsWithoutExistingServers(bool hasServers)
    {
        if (hasServers)
            await _refresh.RefreshCommand.ExecuteAsync(null);

        var pending = new TaskCompletionSource<HubApi.HubServerEntry[]>();
        _api.Respond = () => pending.Task;
        var notifications = new List<string?>();
        _servers.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        var refresh = _refresh.RefreshCommand.ExecuteAsync(null);

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(_servers.IsLoadingEmpty, Is.EqualTo(!hasServers));
                Assert.That(_servers.IsEmpty, Is.False);
                Assert.That(_servers.Servers.Count, Is.EqualTo(hasServers ? 1 : 0));
                Assert.That(notifications, Does.Contain(nameof(ServersViewModel.IsLoadingEmpty)));
            });
        }
        finally
        {
            pending.SetResult([Entry("Box", 10)]);
            await refresh;
        }

        Assert.That(_servers.IsLoadingEmpty, Is.False);
    }

    [Test]
    public async Task EmptyFavoritesHintStaysVisibleDuringHubRefresh()
    {
        _favorites.Remove(_favorites.Servers.Single().Id);
        var pending = new TaskCompletionSource<HubApi.HubServerEntry[]>();
        _api.Respond = () => pending.Task;
        var refresh = _refresh.RefreshCommand.ExecuteAsync(null);

        try
        {
            Assert.That(_refresh.IsLoading, Is.True);
            Assert.That(_favoriteList.IsEmpty, Is.True);
        }
        finally
        {
            pending.SetResult([Entry("Box", 10)]);
            await refresh;
        }

        Assert.That(_favoriteList.IsEmpty, Is.True);
    }

    [Test]
    public async Task DisposedListsStopReactingToDirectoryFavoritesAndRefreshChanges()
    {
        await _refresh.RefreshCommand.ExecuteAsync(null);
        _servers.Dispose();
        _favoriteList.Dispose();
        var notifications = 0;
        _servers.PropertyChanged += (_, _) => notifications++;
        _favoriteList.PropertyChanged += (_, _) => notifications++;

        _favorites.Add("ss14://another.example");
        await _refresh.RefreshCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_servers.Servers, Is.Empty);
            Assert.That(_favoriteList.Items, Is.Empty);
            Assert.That(notifications, Is.Zero);
        });
    }

    [Test]
    public async Task ReplacedCardsStopReactingToLanguageChanges()
    {
        await _refresh.RefreshCommand.ExecuteAsync(null);
        var serverCard = _servers.Servers.Single();
        var favoriteCard = _favoriteList.Items.Single();
        var notifications = 0;
        serverCard.PropertyChanged += (_, _) => notifications++;
        favoriteCard.PropertyChanged += (_, _) => notifications++;

        await _refresh.RefreshCommand.ExecuteAsync(null);
        Loc.SetLanguage(Loc.Current);

        Assert.Multiple(() =>
        {
            Assert.That(_servers.Servers.Single(), Is.Not.SameAs(serverCard));
            Assert.That(_favoriteList.Items.Single(), Is.Not.SameAs(favoriteCard));
            Assert.That(notifications, Is.Zero);
        });
    }

    private static HubApi.HubServerEntry Entry(string map, int players) => new()
    {
        Address = Address,
        StatusData = new ServerStatus { Name = "Station", Map = map, Players = players, Tags = ["lang:ru"] },
    };

    private sealed class StubHubApi : IHubApi
    {
        public int Calls { get; private set; }
        public Func<Task<HubApi.HubServerEntry[]>> Respond { get; set; } =
            () => Task.FromResult(new[] { Entry("Box", 10) });

        public Task<HubApi.HubServerEntry[]> GetServers(Uri hubAddress, CancellationToken cancel)
        {
            Calls++;
            return Respond();
        }
    }
}
