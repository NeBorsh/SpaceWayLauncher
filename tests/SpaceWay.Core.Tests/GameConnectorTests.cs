using NSec.Cryptography;
using NUnit.Framework;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

/// <summary>
/// What the game is launched with when joining a server.
/// </summary>
[TestFixture]
public sealed class GameConnectorTests
{
    private const string EngineVersion = "290.0.0";

    private static readonly AuthServer OfficialAuth =
        new(Guid.NewGuid(), "Официальный", new Uri("https://auth.example/"));

    private LauncherDatabase _db = null!;
    private ContentDatabase _content = null!;
    private FakeContentServer _server = null!;
    private HttpClient _http = null!;
    private Key _engineKey = null!;
    private string _tempRoot = null!;
    private AccountManager _accounts = null!;
    private FakeTokenStore _tokens = null!;
    private EngineManager _engines = null!;
    private SettingsStore _settings = null!;
    private GameConnector _connector = null!;
    private ContentLaunchInfo _launch = null!;

    [SetUp]
    public async Task SetUp()
    {
        _tempRoot = TestPaths.CreateTempRoot("connect");
        _db = LauncherDatabase.CreateInMemory();
        _content = ContentDatabase.CreateInMemory();
        _engineKey = Key.Create(SignatureAlgorithm.Ed25519);

        _server = new FakeContentServer { EnginePayload = "движок"u8.ToArray() };
        _server.AddFile("Content.Client.dll", "сборки");
        _http = new HttpClient(_server);

        _tokens = new FakeTokenStore();
        _accounts = new AccountManager(
            new AccountStore(_db), _tokens, new FakeAuthApi(), new SettingsStore(_db));
        _accounts.SaveServer(OfficialAuth);
        new AccountStore(_db).EnsureOfflineServer();

        _engines = new EngineManager(
            new EngineStore(_db),
            new StubManifests(_server.EngineManifest(_engineKey, EngineVersion)),
            _http,
            new EngineSignature(_engineKey.PublicKey),
            Path.Combine(_tempRoot, "engines"),
            Path.Combine(_tempRoot, "modules"));

        var updater = new ContentUpdater(_content, _engines, _http);

        _settings = new SettingsStore(_db);

        _connector = new GameConnector(
            new StubServerApi(Info), updater, _engines, _accounts,
            new PrivacyPolicyStore(_db),
            _settings,
            loaderPath: Path.Combine(_tempRoot, "SpaceWay.Loader.exe"));

        _launch = await updater.Prepare(BuildInfo());
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _engineKey.Dispose();
        _content.KeepAlive?.Dispose();
        _db.Dispose();
        TestPaths.DeleteQuietly(_tempRoot);
    }

    [Test]
    public void FirstArgumentsAreEngineAndItsSignature()
    {
        var plan = Plan();

        Assert.Multiple(() =>
        {
            Assert.That(plan.Arguments[0], Does.EndWith($"{EngineVersion}.zip"));
            Assert.That(plan.Arguments[1], Is.EqualTo(_engines.GetEngineSignature(EngineVersion)));
        });
    }

    [Test]
    public void UsernameAndLauncherFlagsArePassed()
    {
        var account = _accounts.CreateOfflineAccount("Атмосник");

        var plan = Plan(account: account);

        Assert.Multiple(() =>
        {
            Assert.That(CVarValue(plan, "--username"), Is.EqualTo("Атмосник"));
            Assert.That(plan.Arguments, Does.Contain("--launcher"));
            Assert.That(plan.Arguments, Does.Contain("launch.launcher=true"));
        });
    }

    [Test]
    public void WithoutAccountFallbackNameIsUsed()
    {
        Assert.That(CVarValue(Plan(), "--username"), Is.EqualTo(GameConnector.FallbackUsername));
    }

    [Test]
    public void ConnectAddressFallsBackToServerAddress()
    {
        var plan = Plan(info: Info() with { ConnectAddress = null });

        Assert.That(CVarValue(plan, "--connect-address"), Is.EqualTo("udp://игровой.example:1212/"));
    }

    [Test]
    public void ServerCanPointConnectionElsewhere()
    {
        var plan = Plan(info: Info() with { ConnectAddress = "udp://другой.example:5000" });

        Assert.That(CVarValue(plan, "--connect-address"), Is.EqualTo("udp://другой.example:5000/"));
    }

    [Test]
    public void BuildInfoIsPassedToEngine()
    {
        var plan = Plan();

        Assert.Multiple(() =>
        {
            Assert.That(plan.Arguments, Does.Contain($"build.engine_version={EngineVersion}"));
            Assert.That(plan.Arguments, Does.Contain("build.fork_id=тест"));
            Assert.That(plan.Arguments, Does.Contain($"build.manifest_hash={BuildInfo().ManifestHash}"));
        });
    }

    [Test]
    public void EmptyBuildFieldsAreNotPassed()
    {
        var plan = Plan();

        Assert.That(plan.Arguments, Has.None.StartWith("build.hash="));
    }

    [Test]
    public void ContentIsPassedToLoaderThroughEnvironment()
    {
        var plan = Plan();

        Assert.Multiple(() =>
        {
            Assert.That(plan.Environment["SPACEWAY_CONTENT_VERSION"],
                Is.EqualTo(_launch.VersionId.ToString()));
            Assert.That(plan.Environment, Contains.Key("SPACEWAY_CONTENT_DB"));
            Assert.That(plan.Environment, Contains.Key("SPACEWAY_LAUNCHER_PATH"));
        });
    }

    [Test]
    public void TokenIsPassedForAuthorisedAccount()
    {
        var account = CreateOnlineAccount();

        var plan = Plan(account: account, token: new AuthToken("токен", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Environment["ROBUST_AUTH_TOKEN"], Is.EqualTo("токен"));
            Assert.That(plan.Environment["ROBUST_AUTH_USERID"], Is.EqualTo(account.UserId.ToString()));
            Assert.That(plan.Environment["ROBUST_AUTH_SERVER"], Is.EqualTo(OfficialAuth.Address.ToString()));
            Assert.That(plan.Environment["ROBUST_AUTH_PUBKEY"], Is.EqualTo("КЛЮЧ СЕРВЕРА"));
        });
    }

    [Test]
    public void OfflineAccountGetsNoToken()
    {
        var account = _accounts.CreateOfflineAccount("Атмосник");

        var plan = Plan(account: account, token: new AuthToken("токен", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Environment, Does.Not.ContainKey("ROBUST_AUTH_TOKEN"));
            Assert.That(CVarValue(plan, "--username"), Is.EqualTo("Атмосник"));
        });
    }

    [Test]
    public void ServerWithAuthDisabledGetsNoToken()
    {
        var account = CreateOnlineAccount();
        var info = Info() with { Auth = new ServerAuthInfo { Mode = AuthMode.Disabled } };

        var plan = Plan(info: info, account: account,
            token: new AuthToken("токен", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.That(plan.Environment, Does.Not.ContainKey("ROBUST_AUTH_TOKEN"));
    }

    [Test]
    public void CompatCVarIsOffByDefault()
    {
        Assert.That(Plan().Arguments, Does.Not.Contain("display.compat=true"));
    }

    [Test]
    public void CompatCVarIsPassedWhenAsked()
    {
        _settings.SetBool(SettingKeys.DisplayCompat, true);

        var arguments = Plan().Arguments.ToList();
        var index = arguments.IndexOf("display.compat=true");

        Assert.That(index, Is.GreaterThan(0), "cvar was not passed");

        Assert.That(arguments[index - 1], Is.EqualTo("--cvar"));
    }

    [Test]
    public void WithoutMods_LoaderGetsNoOverlay()
    {
        Assert.That(Plan().Environment, Does.Not.ContainKey("SPACEWAY_OVERLAY_ZIP"));
    }

    [Test]
    public void OverlayPath_ReachesLoader()
    {
        var overlay = Path.Combine(_tempRoot, "overlay.zip");

        var plan = _connector.BuildLaunchPlan(
            ServerUri(), Info(), _launch, account: null, token: null, overlayPath: overlay);

        Assert.That(plan.Environment["SPACEWAY_OVERLAY_ZIP"], Is.EqualTo(overlay));
    }

    [Test]
    public void MissingLoaderIsReportedClearly()
    {
        var error = Assert.ThrowsAsync<ConnectException>(
            () => _connector.Connect("ss14://игровой.example"));

        Assert.That(error!.Key, Is.EqualTo("error-loader-missing"));
    }

    [Test]
    public async Task ConnectReportsWhatItIsDoing()
    {
        var progress = new RecordingProgress();

        try
        {
            await _connector.Connect("ss14://игровой.example", progress);
        }
        catch (ConnectException)
        {
        }

        Assert.That(progress.Stages, Does.Contain(ContentStage.AskingServer));
    }

    [Test]
    public void UnparsableAddressIsRefused()
    {
        Assert.ThrowsAsync<ConnectException>(() => _connector.Connect("http://не-тот-протокол"));
    }

    [Test]
    public void BundleOpensItselfInsteadOfConnecting()
    {
        var plan = _connector.BuildBundleLaunchPlan(_launch, bundleOverlay: null);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Arguments, Does.Contain("launch.content_bundle=true"));

            Assert.That(plan.Arguments, Does.Not.Contain("--launcher"));
            Assert.That(plan.Arguments, Does.Not.Contain("--connect-address"));
        });
    }

    [Test]
    public void BundleGetsNoToken()
    {
        var account = CreateOnlineAccount();
        _accounts.Select(account);

        var plan = _connector.BuildBundleLaunchPlan(_launch, bundleOverlay: null);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Environment, Does.Not.ContainKey("ROBUST_AUTH_TOKEN"));
            Assert.That(plan.Arguments, Does.Contain(account.Username));
        });
    }

    [Test]
    public void BundleAndModsAreSeparateOverlays()
    {
        var bundle = Path.Combine(_tempRoot, "replay.zip");
        var mods = Path.Combine(_tempRoot, "overlay.zip");

        var plan = _connector.BuildBundleLaunchPlan(_launch, bundle, mods);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Environment["SPACEWAY_BUNDLE_ZIP"], Is.EqualTo(bundle));
            Assert.That(plan.Environment["SPACEWAY_OVERLAY_ZIP"], Is.EqualTo(mods));
            Assert.That(plan.Environment["SPACEWAY_CONTENT_VERSION"], Is.EqualTo(_launch.VersionId.ToString()));
        });
    }

    [Test]
    public void ServerGcIsPassedOnlyWhenAsked()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_connector.BuildBundleLaunchPlan(_launch, null).Environment,
                Does.Not.ContainKey("DOTNET_gcServer"));
            Assert.That(_connector.BuildBundleLaunchPlan(_launch, null, serverGc: true).Environment["DOTNET_gcServer"],
                Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task ReplayGoesAllTheWayToLaunch()
    {
        var build = BuildInfo();
        var metadata = $$"""
            {
              "engine_version": "{{EngineVersion}}",
              "base_build": {
                "fork_id": "{{build.ForkId}}",
                "version": "{{build.Version}}",
                "manifest_url": "{{build.ManifestUrl}}",
                "manifest_download_url": "{{build.ManifestDownloadUrl}}",
                "manifest_hash": "{{build.ManifestHash}}"
              }
            }
            """;
        var path = ContentBundleTests.Write(_tempRoot, "replay.zip", metadata, ("replay/data.yml", "раунд"));
        var progress = new RecordingProgress();

        var error = Assert.ThrowsAsync<ConnectException>(() => _connector.LaunchBundle(path, progress));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Key, Is.EqualTo("error-loader-missing"));
            Assert.That(progress.Stages, Does.Contain(ContentStage.OpeningBundle));
        });

        await Task.CompletedTask;
    }

    [Test]
    public void ReplayOfVanishedBuildIsExplained()
    {
        var metadata = $$"""
            {
              "engine_version": "{{EngineVersion}}",
              "base_build": {
                "fork_id": "wizards",
                "version": "давняя",
                "manifest_url": "https://gone.example/manifest.txt",
                "manifest_download_url": "https://gone.example/download",
                "manifest_hash": "AABB"
              }
            }
            """;
        var path = ContentBundleTests.Write(_tempRoot, "replay.zip", metadata, ("replay/data.yml", "раунд"));

        var error = Assert.ThrowsAsync<ContentUpdateException>(() => _connector.LaunchBundle(path));

        Assert.That(error!.Key, Is.EqualTo("error-bundle-base-unavailable"));
    }

    [Test]
    public void NotABundleIsRefusedBeforeAnyDownload()
    {
        var path = ContentBundleTests.Write(_tempRoot, "photos.zip", metadata: null, ("cat.png", "мяу"));
        var before = _server.FilesSent;

        var error = Assert.ThrowsAsync<ContentUpdateException>(() => _connector.LaunchBundle(path));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Key, Is.EqualTo("error-not-a-bundle"));
            Assert.That(_server.FilesSent, Is.EqualTo(before));
        });
    }

    private GameLaunchPlan Plan(ServerInfo? info = null, Account? account = null, AuthToken? token = null) =>
        _connector.BuildLaunchPlan(ServerUri(), info ?? Info(), _launch, account, token);

    private Account CreateOnlineAccount()
    {
        var account = new Account(Guid.NewGuid(), "Игрок", OfficialAuth.Id, DateTimeOffset.UtcNow.AddDays(30));
        new AccountStore(_db).SaveAccount(account);
        _accounts.Reload();
        return account;
    }

    private static Uri ServerUri()
    {
        ServerAddress.TryParse("ss14://игровой.example", out var uri);
        return uri;
    }

    private ServerBuildInformation BuildInfo() => _server.BuildInfoWithManifest(EngineVersion);

    private ServerInfo Info() => new()
    {
        Build = BuildInfo(),
        Auth = new ServerAuthInfo { Mode = AuthMode.Optional, PublicKey = "КЛЮЧ СЕРВЕРА" },
        ConnectAddress = null,
    };

    /// <summary>Value following the given key.</summary>
    private static string? CVarValue(GameLaunchPlan plan, string key)
    {
        var index = plan.Arguments.ToList().IndexOf(key);
        return index < 0 || index + 1 >= plan.Arguments.Count ? null : plan.Arguments[index + 1];
    }

    /// <summary>
    /// Records progress messages immediately, on the same thread.
    /// </summary>
    private sealed class RecordingProgress : IProgress<ContentProgress>
    {
        private readonly List<ContentStage> _stages = [];

        public IReadOnlyList<ContentStage> Stages
        {
            get
            {
                lock (_stages)
                    return _stages.ToList();
            }
        }

        public void Report(ContentProgress value)
        {
            lock (_stages)
                _stages.Add(value.Stage);
        }
    }

    private sealed class StubServerApi(Func<ServerInfo> info) : IServerApi
    {
        public Task<ServerInfo> GetInfo(Uri serverAddress, CancellationToken cancel = default) =>
            Task.FromResult(info());
    }

    private sealed class StubManifests(EngineBuildManifest builds) : IEngineManifestSource
    {
        public Task<EngineBuildManifest> GetBuilds(CancellationToken cancel = default) =>
            Task.FromResult(builds);

        public Task<EngineModuleManifest> GetModules(CancellationToken cancel = default) =>
            Task.FromResult(new EngineModuleManifest([]));
    }
}
