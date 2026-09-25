using NSec.Cryptography;
using NUnit.Framework;
using SpaceWay.Core.Accounts;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Consent to a server's privacy policy.
/// </summary>
[TestFixture]
public sealed class PrivacyPolicyTests
{
    private static readonly ServerPrivacyPolicy Policy = new(
        "https://example.org/policy", "atmosia", "2");

    private LauncherDatabase _db = null!;
    private ContentDatabase _content = null!;
    private FakeContentServer _server = null!;
    private HttpClient _http = null!;
    private Key _engineKey = null!;
    private string _tempRoot = null!;
    private PrivacyPolicyStore _policies = null!;
    private RecordingPrompt _prompt = null!;
    private GameConnector _connector = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = TestPaths.CreateTempRoot("privacy");
        _db = LauncherDatabase.CreateInMemory();
        _content = ContentDatabase.CreateInMemory();
        _engineKey = Key.Create(SignatureAlgorithm.Ed25519);
        _policies = new PrivacyPolicyStore(_db);
        _prompt = new RecordingPrompt();

        _server = new FakeContentServer { EnginePayload = "движок"u8.ToArray() };
        _server.AddFile("Content.Client.dll", "сборки");
        _http = new HttpClient(_server);

        var accounts = new AccountManager(
            new AccountStore(_db), new FakeTokenStore(), new FakeAuthApi(), new SettingsStore(_db));

        var engines = new EngineManager(
            new EngineStore(_db),
            new StubManifests(_server.EngineManifest(_engineKey)),
            _http,
            new EngineSignature(_engineKey.PublicKey),
            Path.Combine(_tempRoot, "engines"),
            Path.Combine(_tempRoot, "modules"));

        _connector = new GameConnector(
            new StubServerApi(() => Info(Policy)),
            new ContentUpdater(_content, engines, _http),
            engines,
            accounts,
            _policies,
            new SettingsStore(_db),
            loaderPath: Path.Combine(_tempRoot, "SpaceWay.Loader.exe"));
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
    public async Task PolicyIsAskedAboutAndRemembered()
    {
        _prompt.Answer = true;

        await Connect();

        Assert.Multiple(() =>
        {
            Assert.That(_prompt.Asked, Is.EqualTo(1));
            Assert.That(_policies.AcceptedVersion("atmosia"), Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task SecondConnectionDoesNotAskAgain()
    {
        _prompt.Answer = true;
        await Connect();

        await Connect();

        Assert.That(_prompt.Asked, Is.EqualTo(1), "asked again about something already accepted");
    }

    [Test]
    public void DeclineStopsConnection()
    {
        _prompt.Answer = false;

        var error = Assert.ThrowsAsync<ConnectException>(() => _connector.Connect("ss14://игровой.example"));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Message, Does.Contain("политикой приватности"));
            Assert.That(_policies.AcceptedVersion("atmosia"), Is.Null, "a refusal was recorded as consent");
        });
    }

    [Test]
    public void DeclineHappensBeforeDownloading()
    {
        _prompt.Answer = false;

        Assert.ThrowsAsync<ConnectException>(() => _connector.Connect("ss14://игровой.example"));

        Assert.That(_server.DownloadRequests, Is.Zero, "content was requested too early");
    }

    [Test]
    public async Task ChangedVersionIsAskedAboutAgain()
    {
        _policies.Accept("atmosia", "1");
        _prompt.Answer = true;

        await Connect();

        Assert.Multiple(() =>
        {
            Assert.That(_prompt.Asked, Is.EqualTo(1));
            Assert.That(_prompt.LastVersionChanged, Is.True, "the player was not told the terms changed");
            Assert.That(_policies.AcceptedVersion("atmosia"), Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task ServerWithoutPolicyIsNotAskedAbout()
    {
        var connector = ConnectorFor(policy: null);

        try
        {
            await connector.Connect("ss14://игровой.example");
        }
        catch (ConnectException)
        {
        }

        Assert.That(_prompt.Asked, Is.Zero);
    }

    [Test]
    public void WithoutPromptConnectionIsRefused()
    {
        var error = Assert.ThrowsAsync<ConnectException>(
            () => _connector.Connect("ss14://игровой.example", privacyPrompt: null));

        Assert.That(error!.Key, Is.EqualTo("error-privacy-nobody-to-ask"));
    }

    /// <summary>Drives a connection to completion, except for the missing loader.</summary>
    private async Task Connect()
    {
        try
        {
            await _connector.Connect("ss14://игровой.example", privacyPrompt: _prompt);
        }
        catch (ConnectException e) when (e.Key == "error-loader-missing")
        {
        }
    }

    private GameConnector ConnectorFor(ServerPrivacyPolicy? policy)
    {
        var engines = new EngineManager(
            new EngineStore(_db),
            new StubManifests(_server.EngineManifest(_engineKey)),
            _http,
            new EngineSignature(_engineKey.PublicKey),
            Path.Combine(_tempRoot, "engines"),
            Path.Combine(_tempRoot, "modules"));

        return new GameConnector(
            new StubServerApi(() => Info(policy)),
            new ContentUpdater(_content, engines, _http),
            engines,
            new AccountManager(
                new AccountStore(_db), new FakeTokenStore(), new FakeAuthApi(), new SettingsStore(_db)),
            _policies,
            new SettingsStore(_db),
            loaderPath: Path.Combine(_tempRoot, "SpaceWay.Loader.exe"));
    }

    private ServerInfo Info(ServerPrivacyPolicy? policy) => new()
    {
        Build = _server.BuildInfoWithManifest(),
        Auth = new ServerAuthInfo { Mode = AuthMode.Optional },
        PrivacyPolicy = policy,
    };

    private sealed class RecordingPrompt : IPrivacyPolicyPrompt
    {
        public bool Answer { get; set; }

        public int Asked { get; private set; }

        public bool LastVersionChanged { get; private set; }

        public Task<bool> Ask(
            ServerPrivacyPolicy policy, bool versionChanged, CancellationToken cancel = default)
        {
            Asked++;
            LastVersionChanged = versionChanged;
            return Task.FromResult(Answer);
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
