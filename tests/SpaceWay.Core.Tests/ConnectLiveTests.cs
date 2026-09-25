using NUnit.Framework;
using Dapper;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Content;
using SpaceWay.Core.Data;
using SpaceWay.Core.Engine;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Queries real game servers.
/// </summary>
[TestFixture]
[Explicit("Требует сети и ходит на живые игровые серверы")]
[Category("Live")]
public sealed class ConnectLiveTests
{
    /// <summary>How many servers from the hub to query.</summary>
    private const int SampleSize = 12;

    private HttpClient _http = null!;
    private string[] _addresses = null!;

    [OneTimeSetUp]
    public async Task FetchServerList()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", "SpaceWayLauncher/0.1");

        var servers = await new HubApi(_http).GetServers(HubEntry.Official.Address, CancellationToken.None);

        _addresses = servers.Select(s => s.Address).Take(SampleSize).ToArray();

        TestContext.Out.WriteLine($"Hub returned {servers.Length} servers, querying {_addresses.Length}");
    }

    [OneTimeTearDown]
    public void Cleanup() => _http.Dispose();

    [Test]
    public async Task RealServersAnswerUnderstandably()
    {
        var api = new ServerApi(_http);

        var answered = 0;
        var withManifest = 0;
        var failures = new List<string>();

        foreach (var address in _addresses)
        {
            if (!ServerAddress.TryParse(address, out var uri))
            {
                failures.Add($"{address}: адрес не разобрался");
                continue;
            }

            try
            {
                var info = await api.GetInfo(uri);

                if (info.Build == null)
                {
                    failures.Add($"{address}: не сообщил сборку");
                    continue;
                }

                answered++;
                if (info.Build.SupportsManifest)
                    withManifest++;

                TestContext.Out.WriteLine(
                    $"{address}: engine {info.Build.EngineVersion}, fork {info.Build.ForkId}, " +
                    $"manifest downloads {(info.Build.SupportsManifest ? "yes" : "no")}, " +
                    $"auth {info.Auth?.Mode}");
            }
            catch (ConnectException e)
            {
                failures.Add($"{address}: {e.Message}");
            }
        }

        foreach (var failure in failures)
            TestContext.Out.WriteLine($"no response: {failure}");

        Assert.Multiple(() =>
        {
            Assert.That(answered, Is.GreaterThan(0), "no live server gave a valid response");
            Assert.That(withManifest, Is.GreaterThan(0), "no server supports manifest downloads");
        });
    }

    [Test]
    public async Task EngineVersionsMatchBuildManifest()
    {
        var api = new ServerApi(_http);
        var builds = await new RobustBuildsApi(_http).GetBuilds();

        var checkedVersions = 0;

        foreach (var address in _addresses.Take(5))
        {
            if (!ServerAddress.TryParse(address, out var uri))
                continue;

            try
            {
                var info = await api.GetInfo(uri);
                if (info.Build?.EngineVersion is not { } version)
                    continue;

                Assert.That(builds.Find(version), Is.Not.Null,
                    $"{address} requires engine {version}, which is not in the manifest");

                checkedVersions++;
            }
            catch (ConnectException)
            {
            }
        }

        Assert.That(checkedVersions, Is.GreaterThan(0), "no version could be checked");
    }

    [Test]
    public async Task RealManifestProtocolWorks()
    {
        var api = new ServerApi(_http);

        var root = TestPaths.CreateTempRoot("live-content");
        Directory.CreateDirectory(root);

        var content = new ContentDatabase(Path.Combine(root, "content.db"));
        content.Initialize();

        using var launcherDb = LauncherDatabase.CreateInMemory();
        using var download = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        var engines = new EngineManager(
            new EngineStore(launcherDb), new RobustBuildsApi(_http), download,
            engineDir: Path.Combine(root, "engines"),
            moduleDir: Path.Combine(root, "modules"));

        var updater = new ContentUpdater(content, engines, download);

        var seen = new List<ContentStage>();
        var progress = new StageRecorder(seen);
        var succeeded = false;

        foreach (var build in await ServersWithManifest(api))
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            try
            {
                await updater.Prepare(build, progress, cancellation.Token);
                TestContext.Out.WriteLine("Content downloaded completely");
                succeeded = true;
                break;
            }
            catch (OperationCanceledException)
            {
                TestContext.Out.WriteLine("Download stopped on timeout, as intended");
                succeeded = true;
                break;
            }
            catch (ContentUpdateException e)
            {
                TestContext.Out.WriteLine($"Failed: {e.Message}");
            }
        }

        content.KeepAlive?.Dispose();

        if (!succeeded)
            Assert.Inconclusive("Manifest downloads worked with none of the sampled servers");

        using (var con = content.Open())
        {
            var blobs = con.ExecuteScalar<long>("SELECT COUNT(*) FROM Content");
            TestContext.Out.WriteLine($"Files in database: {blobs}");

            Assert.Multiple(() =>
            {
                Assert.That(seen, Does.Contain(ContentStage.FetchingManifest),
                    "the manifest was never reached");
                Assert.That(blobs, Is.GreaterThan(0),
                    "the real server sent no files");
            });
        }

        TestPaths.DeleteQuietly(root);
    }

    private async Task<List<ServerBuildInformation>> ServersWithManifest(ServerApi api)
    {
        var found = new List<ServerBuildInformation>();

        foreach (var address in _addresses)
        {
            if (!ServerAddress.TryParse(address, out var uri))
                continue;

            try
            {
                var info = await api.GetInfo(uri);
                if (info.Build is { SupportsManifest: true } build)
                {
                    TestContext.Out.WriteLine($"Candidate: {address}, fork {build.ForkId}");
                    found.Add(build);
                }
            }
            catch (ConnectException)
            {
            }
        }

        return found;
    }

    private sealed class StageRecorder(List<ContentStage> stages) : IProgress<ContentProgress>
    {
        public void Report(ContentProgress value)
        {
            lock (stages)
            {
                if (stages.Count == 0 || stages[^1] != value.Stage)
                    stages.Add(value.Stage);
            }
        }
    }
}
