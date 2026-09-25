using SpaceWay.Core;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;
using SpaceWay.Core.Favorites;
using SpaceWay.Core.Hubs;
using SpaceWay.Core.Localization;
using SpaceWay.Core.Mods;

namespace SpaceWay.Launcher;

/// <summary>
/// Composition root. Wired by hand: a DI container for a dozen objects
/// is not worth it.
/// </summary>
public sealed class LauncherServices : IDisposable
{
    private const string UserAgent = "SpaceWayLauncher/0.1";

    public LauncherServices(string? databasePath = null)
    {
        Database = new LauncherDatabase(databasePath ?? LauncherPaths.PathDataDb);

        Settings = new SettingsStore(Database);
        Hubs = new HubStore(Database);
        Accounts = new AccountStore(Database);
        Favorites = new FavoritesStore(Database);
        PrivacyPolicies = new PrivacyPolicyStore(Database);
        FavoritesService = new FavoritesService(Favorites);
        Mods = new ModLibrary(new ModStore(Database));

        Hubs.SeedIfEmpty();
        Accounts.SeedIfEmpty();

        Accounts.EnsureOfflineServer();

        Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        Http.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        ServerList = new ServerListService(new HubApi(Http));
        Directory = new ServerDirectory(ServerList);
        HubManager = new HubManager(Hubs);

        DownloadHttp = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        DownloadHttp.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        EngineStore = new EngineStore(Database);
        EngineManager = new EngineManager(EngineStore, new RobustBuildsApi(Http), DownloadHttp);

        ContentDatabase = new ContentDatabase();
        ContentDatabase.Initialize();
        ContentUpdater = new ContentUpdater(ContentDatabase, EngineManager, DownloadHttp);

        Tokens = TokenStoreFactory.Create(LauncherPaths.DirUserData);
        AccountManager = new AccountManager(Accounts, Tokens, new AuthApi(Http), Settings);

        ModOverlay = new ModOverlay(Mods);

        ServerApi = new ServerApi(Http);
        ServerInfo = new ServerInfoCache(ServerApi);

        Connector = new GameConnector(
            ServerApi, ContentUpdater, EngineManager, AccountManager, PrivacyPolicies,
            Settings, ModOverlay);

        FavoritesService.Reload();
        HubManager.Reload();
        AccountManager.Reload();
    }

    public LauncherDatabase Database { get; }
    public SettingsStore Settings { get; }
    public HubStore Hubs { get; }
    public AccountStore Accounts { get; }
    public FavoritesStore Favorites { get; }
    public PrivacyPolicyStore PrivacyPolicies { get; }
    public FavoritesService FavoritesService { get; }
    public ModLibrary Mods { get; }

    public ModOverlay ModOverlay { get; }
    public HttpClient Http { get; }

    /// <summary>Client for long downloads: engine, modules, content.</summary>
    public HttpClient DownloadHttp { get; }
    public ServerListService ServerList { get; }
    public ServerDirectory Directory { get; }
    public HubManager HubManager { get; }
    public EngineStore EngineStore { get; }
    public EngineManager EngineManager { get; }
    public ContentDatabase ContentDatabase { get; }
    public ContentUpdater ContentUpdater { get; }
    public GameConnector Connector { get; }
    public IServerApi ServerApi { get; }

    /// <summary>Server descriptions and links for cards.</summary>
    public ServerInfoCache ServerInfo { get; }
    public ITokenStore Tokens { get; }
    public AccountManager AccountManager { get; }

    /// <summary>Applies the saved language, if any.</summary>
    public void ApplySavedLanguage()
    {
        var saved = Settings.Get(SettingKeys.Language);
        Loc.SetLanguage(saved ?? Loc.DefaultLanguage);
    }

    public void Dispose()
    {
        DownloadHttp.Dispose();
        Http.Dispose();
        Database.Dispose();
    }
}
