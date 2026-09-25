using System.Net;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SpaceWay.Core.Updates;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class LauncherVersionTests
{
    [Test]
    [TestCase("0.2.0", "0.2.0")]
    [TestCase("v0.2.0", "0.2.0")]
    [TestCase("V1.10.3", "1.10.3")]
    [TestCase("0.2", "0.2.0")]
    [TestCase(" 0.2.1 ", "0.2.1")]
    [TestCase("0.3.0-beta", "0.3.0")]
    [TestCase("0.3.0+abc123", "0.3.0")]
    public void Tag_IsParsed(string tag, string expected)
    {
        Assert.That(LauncherVersion.TryParse(tag, out var version), Is.True);
        Assert.That(version!.ToString(3), Is.EqualTo(expected));
    }

    [Test]
    [TestCase("")]
    [TestCase("latest")]
    [TestCase("v")]
    [TestCase("1")]
    public void Garbage_IsRejected(string tag)
    {
        Assert.That(LauncherVersion.TryParse(tag, out _), Is.False);
    }

    [Test]
    public void Comparison_IsNumeric()
    {
        Assert.That(LauncherVersion.TryParse("0.10.0", out var newer), Is.True);
        Assert.That(LauncherVersion.TryParse("0.9.0", out var older), Is.True);

        Assert.That(newer, Is.GreaterThan(older!));
    }
}

[TestFixture]
public sealed class GitHubReleasesTests
{
    private readonly GitHubReleases _releases = new(new HttpClient(), "owner/repo");

    [Test]
    [TestCase("0.2.0")]
    [TestCase("v0.2.0")]
    public void ReleasePage_GivesVersionAndDownloads(string tag)
    {
        var release = _releases.FromReleasePage(new Uri($"https://github.com/owner/repo/releases/tag/{tag}"));

        Assert.That(release, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(release!.Version, Is.EqualTo(new Version(0, 2, 0)));
            Assert.That(release.Tag, Is.EqualTo(tag));
            Assert.That(release.FileUrl(release.WindowsInstallerName).AbsoluteUri, Is.EqualTo(
                $"https://github.com/owner/repo/releases/download/{tag}/SpaceWayLauncher-0.2.0-setup.exe"));
        });
    }

    [Test]
    [TestCase("https://github.com/owner/repo/releases")]
    [TestCase("https://github.com/owner/repo/releases/tag/nightly")]
    [TestCase("https://github.com/other/repo/releases/tag/0.2.0")]
    public void OtherPages_AreNotReleases(string page)
    {
        Assert.That(_releases.FromReleasePage(new Uri(page)), Is.Null);
    }

    [Test]
    public void Checksums_AreParsed()
    {
        var hash = new string('a', 64);
        var upper = new string('B', 64);

        var parsed = GitHubReleases.ParseChecksums(
            $"{hash}  SpaceWayLauncher-0.2.0-setup.exe\r\n" +
            $"{upper} *SpaceWayLauncher-0.2.0-linux-x64.tar.gz\n" +
            "garbage line\n" +
            "\n");

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Has.Count.EqualTo(2));
            Assert.That(parsed["SpaceWayLauncher-0.2.0-setup.exe"], Is.EqualTo(hash));
            Assert.That(parsed["SpaceWayLauncher-0.2.0-linux-x64.tar.gz"], Is.EqualTo(upper.ToLowerInvariant()));
        });
    }
}

[TestFixture]
public sealed class UpdateServiceTests
{
    private static readonly Version Current = new(0, 1, 0);
    private static readonly byte[] Installer = Encoding.UTF8.GetBytes("new installer");

    private string _dir = null!;
    private FakeReleaseServer _server = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = TestPaths.CreateTempRoot("updates");
        _server = new FakeReleaseServer();
    }

    [TearDown]
    public void TearDown() => TestPaths.DeleteQuietly(_dir);

    [Test]
    public async Task SameVersion_IsUpToDate()
    {
        var service = Create("0.1.0", canSelfUpdate: true);

        await service.Check();

        Assert.That(service.State, Is.EqualTo(UpdateState.UpToDate));
        Assert.That(_server.Requests, Is.Empty);
    }

    [Test]
    public async Task NoReleases_IsUpToDate()
    {
        var service = new UpdateService(new StaticReleases(null), new HttpClient(_server), _dir, true, Current);

        await service.Check();

        Assert.That(service.State, Is.EqualTo(UpdateState.UpToDate));
    }

    [Test]
    public async Task PortableCopy_OnlyReportsUpdate()
    {
        var service = Create("0.2.0", canSelfUpdate: false);

        await service.Check();

        Assert.That(service.State, Is.EqualTo(UpdateState.Available));
        Assert.That(_server.Requests, Is.Empty, "a portable copy must not download the installer");
    }

    [Test]
    public async Task InstalledCopy_DownloadsVerifiedInstaller()
    {
        var service = Create("v0.2.0", canSelfUpdate: true);

        await service.Check();

        Assert.That(service.State, Is.EqualTo(UpdateState.Ready));
        Assert.That(File.ReadAllBytes(service.InstallerPath!), Is.EqualTo(Installer));
        Assert.That(Path.GetFileName(service.InstallerPath), Is.EqualTo("SpaceWayLauncher-0.2.0-setup.exe"));
    }

    [Test]
    public async Task TamperedInstaller_IsRejected()
    {
        _server.ServedInstaller = Encoding.UTF8.GetBytes("tampered");
        var service = Create("0.2.0", canSelfUpdate: true);

        await service.Check();

        Assert.That(service.State, Is.EqualTo(UpdateState.Failed));
        Assert.That((service.Error as UpdateException)?.Key, Is.EqualTo("update-error-checksum-mismatch"));
        Assert.That(Directory.EnumerateFiles(_dir), Is.Empty, "a rejected download must not stay on disk");
    }

    [Test]
    public async Task MissingChecksum_IsRejected()
    {
        _server.Checksums = "";
        var service = Create("0.2.0", canSelfUpdate: true);

        await service.Check();

        Assert.That(service.State, Is.EqualTo(UpdateState.Failed));
        Assert.That((service.Error as UpdateException)?.Key, Is.EqualTo("update-error-no-checksum"));
    }

    [Test]
    public async Task VerifiedDownload_IsReused()
    {
        await Create("0.2.0", canSelfUpdate: true).Check();
        var requests = _server.Requests.Count;

        var again = Create("0.2.0", canSelfUpdate: true);
        await again.Check();

        Assert.That(again.State, Is.EqualTo(UpdateState.Ready));
        Assert.That(_server.Requests, Has.Count.EqualTo(requests + 1), "only the checksum list is fetched again");
    }

    [Test]
    public async Task UpToDate_RemovesOldInstallers()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "SpaceWayLauncher-0.1.0-setup.exe"), "old");

        await Create("0.1.0", canSelfUpdate: true).Check();

        Assert.That(Directory.EnumerateFiles(_dir), Is.Empty);
    }

    private UpdateService Create(string tag, bool canSelfUpdate)
    {
        var release = new GitHubReleases(new HttpClient(), "owner/repo")
            .FromReleasePage(new Uri($"https://github.com/owner/repo/releases/tag/{tag}"));

        return new UpdateService(new StaticReleases(release), new HttpClient(_server), _dir, canSelfUpdate, Current);
    }

    private sealed class StaticReleases(LauncherRelease? release) : IReleaseSource
    {
        public Task<LauncherRelease?> GetLatest(CancellationToken cancel = default) => Task.FromResult(release);
    }

    private sealed class FakeReleaseServer : HttpMessageHandler
    {
        public FakeReleaseServer()
        {
            Checksums = $"{Convert.ToHexStringLower(SHA256.HashData(Installer))}  SpaceWayLauncher-0.2.0-setup.exe\n";
        }

        public string Checksums { get; set; }

        public byte[] ServedInstaller { get; set; } = Installer;

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);

            var response = path.EndsWith("/SHA256SUMS")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Checksums) }
                : path.EndsWith("-setup.exe")
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(ServedInstaller) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);

            return Task.FromResult(response);
        }
    }
}
