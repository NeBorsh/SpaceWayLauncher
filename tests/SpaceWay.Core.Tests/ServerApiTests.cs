using System.Net;
using System.Text;
using NUnit.Framework;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ServerApiTests
{
    [Test]
    public async Task ReadsBuildAndAuthInfo()
    {
        var api = ApiReturning("""
            {
              "connect_address": "udp://game.example:1212",
              "auth": { "mode": "Required", "public_key": "КЛЮЧ" },
              "build": {
                "engine_version": "290.0.0",
                "version": "abc123",
                "fork_id": "wizards",
                "download_url": "https://cdn.example/client.zip",
                "hash": "AABB"
              }
            }
            """);

        var info = await api.GetInfo(Parse("ss14://game.example"));

        Assert.Multiple(() =>
        {
            Assert.That(info.Auth!.Mode, Is.EqualTo(AuthMode.Required));
            Assert.That(info.Auth.PublicKey, Is.EqualTo("КЛЮЧ"));
            Assert.That(info.Build!.EngineVersion, Is.EqualTo("290.0.0"));
            Assert.That(info.Build.ForkId, Is.EqualTo("wizards"));
            Assert.That(info.ConnectAddress, Is.EqualTo("udp://game.example:1212"));
        });
    }

    [Test]
    public async Task ReadsDescriptionAndLinks()
    {
        var api = ApiReturning("""
            {
              "desc": "Сервер для атмосов",
              "links": [
                { "name": "Discord", "icon": "discord", "url": "https://discord.gg/example" },
                { "name": "Вики", "url": "https://wiki.example" }
              ]
            }
            """);

        var info = await api.GetInfo(Parse("ss14://game.example"));

        Assert.Multiple(() =>
        {
            Assert.That(info.Description, Is.EqualTo("Сервер для атмосов"));
            Assert.That(info.Links!.Select(l => l.Name), Is.EqualTo(new[] { "Discord", "Вики" }));
            Assert.That(info.Links![0].Icon, Is.EqualTo("discord"));
            Assert.That(info.Links![1].Icon, Is.Null);
        });
    }

    [Test]
    public async Task UnknownFieldsDoNotBreakParsing()
    {
        var api = ApiReturning("""
            {
              "невиданное_поле": 42,
              "build": { "engine_version": "290.0.0", "version": "1", "fork_id": "форк" }
            }
            """);

        var info = await api.GetInfo(Parse("ss14://game.example"));

        Assert.That(info.Build!.ForkId, Is.EqualTo("форк"));
    }

    [Test]
    public async Task AczServerGetsDownloadUrlsInferred()
    {
        var api = ApiReturning("""
            {
              "build": {
                "engine_version": "290.0.0", "version": "1", "fork_id": "своя сборка",
                "acz": true, "manifest_hash": "AABB"
              }
            }
            """);

        var info = await api.GetInfo(Parse("ss14://локалка:5000"));

        Assert.Multiple(() =>
        {
            Assert.That(info.Build!.ManifestUrl, Is.EqualTo("http://локалка:5000/manifest.txt"));
            Assert.That(info.Build.ManifestDownloadUrl, Is.EqualTo("http://локалка:5000/download"));
            Assert.That(info.Build.DownloadUrl, Is.EqualTo("http://локалка:5000/client.zip"));
            Assert.That(info.Build.SupportsManifest, Is.True);
        });
    }

    [Test]
    public async Task ServerWithoutAczGetsOnlyZipUrlInferred()
    {
        var api = ApiReturning("""
            { "build": { "engine_version": "290.0.0", "version": "1", "fork_id": "форк" } }
            """);

        var info = await api.GetInfo(Parse("ss14://локалка:5000"));

        Assert.Multiple(() =>
        {
            Assert.That(info.Build!.DownloadUrl, Is.EqualTo("http://локалка:5000/client.zip"));
            Assert.That(info.Build.ManifestUrl, Is.Null);
            Assert.That(info.Build.SupportsManifest, Is.False);
        });
    }

    [Test]
    public async Task ExplicitDownloadUrlIsLeftAlone()
    {
        var api = ApiReturning("""
            {
              "build": {
                "engine_version": "290.0.0", "version": "1", "fork_id": "форк",
                "download_url": "https://cdn.example/client.zip"
              }
            }
            """);

        var info = await api.GetInfo(Parse("ss14://game.example"));

        Assert.That(info.Build!.DownloadUrl, Is.EqualTo("https://cdn.example/client.zip"));
    }

    [Test]
    public void UnreachableServerGivesUnderstandableError()
    {
        var api = new ServerApi(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        Assert.ThrowsAsync<ConnectException>(() => api.GetInfo(Parse("ss14://game.example")));
    }

    [Test]
    public void GarbageInsteadOfJsonGivesUnderstandableError()
    {
        var api = ApiReturning("<html>сервер отвечает страницей</html>");

        Assert.ThrowsAsync<ConnectException>(() => api.GetInfo(Parse("ss14://game.example")));
    }

    private static Uri Parse(string address)
    {
        ServerAddress.TryParse(address, out var uri);
        return uri;
    }

    private static ServerApi ApiReturning(string json) =>
        new(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        })));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel) =>
            Task.FromResult(respond(request));
    }
}
