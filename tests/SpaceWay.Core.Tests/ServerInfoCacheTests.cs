using NUnit.Framework;
using SpaceWay.Core.Connecting;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ServerInfoCacheTests
{
    [Test]
    public async Task AsksServerOnlyOnce()
    {
        var api = new CountingApi(_ => Task.FromResult(new ServerInfo { Description = "Описание" }));
        var cache = new ServerInfoCache(api);

        await cache.Get("ss14://game.example");
        var info = await cache.Get("ss14://game.example");

        Assert.Multiple(() =>
        {
            Assert.That(info.Description, Is.EqualTo("Описание"));
            Assert.That(api.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SameServerWrittenDifferentlyIsOneEntry()
    {
        var api = new CountingApi(_ => Task.FromResult(new ServerInfo()));
        var cache = new ServerInfoCache(api);

        await cache.Get("ss14://Game.Example:1212/");
        await cache.Get("ss14://game.example");

        Assert.That(api.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task FailureIsNotRemembered()
    {
        var fail = true;
        var api = new CountingApi(async _ =>
        {
            await Task.Yield();
            return fail ? throw new ConnectException("error-server-unreachable") : new ServerInfo();
        });
        var cache = new ServerInfoCache(api);

        Assert.ThrowsAsync<ConnectException>(() => cache.Get("ss14://game.example"));

        fail = false;
        await cache.Get("ss14://game.example");

        Assert.That(api.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task ImmediateFailureIsNotRememberedEither()
    {
        var fail = true;
        var api = new CountingApi(_ => fail
            ? Task.FromException<ServerInfo>(new ConnectException("error-server-unreachable"))
            : Task.FromResult(new ServerInfo()));
        var cache = new ServerInfoCache(api);

        Assert.ThrowsAsync<ConnectException>(() => cache.Get("ss14://game.example"));

        fail = false;
        await cache.Get("ss14://game.example");

        Assert.That(api.Calls, Is.EqualTo(2));
    }

    [Test]
    public void BadAddressGivesUnderstandableError()
    {
        var api = new CountingApi(_ => Task.FromResult(new ServerInfo()));
        var cache = new ServerInfoCache(api);

        var error = Assert.ThrowsAsync<ConnectException>(() => cache.Get("http://не-сервер"));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Key, Is.EqualTo("error-bad-server-address"));
            Assert.That(api.Calls, Is.Zero);
        });
    }

    private sealed class CountingApi(Func<Uri, Task<ServerInfo>> respond) : IServerApi
    {
        public int Calls { get; private set; }

        public Task<ServerInfo> GetInfo(Uri serverAddress, CancellationToken cancel = default)
        {
            Calls++;
            return respond(serverAddress);
        }
    }
}
