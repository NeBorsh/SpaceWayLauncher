using NUnit.Framework;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ServerAddressUriTests
{
    [Test]
    public void SchemeIsOptional()
    {
        Assert.That(ServerAddress.TryParse("атмос.example:1212", out var uri), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(uri.Scheme, Is.EqualTo("ss14"));
            Assert.That(uri.Port, Is.EqualTo(1212));
        });
    }

    [Test]
    public void ForeignSchemeIsRefused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServerAddress.TryParse("http://example.com", out _), Is.False);
            Assert.That(ServerAddress.TryParse("file:///etc/passwd", out _), Is.False);
        });
    }

    [Test]
    public void ApiAddressAddsDefaultPortForPlainScheme()
    {
        ServerAddress.TryParse("ss14://example.com", out var uri);

        Assert.That(ServerAddress.ApiAddress(uri).ToString(), Is.EqualTo("http://example.com:1212/"));
    }

    [Test]
    public void SecureSchemeKeepsItsOwnDefaultPort()
    {
        ServerAddress.TryParse("ss14s://example.com", out var uri);

        Assert.That(ServerAddress.ApiAddress(uri).ToString(), Is.EqualTo("https://example.com/"));
    }

    [Test]
    public void ExplicitPortIsKept()
    {
        ServerAddress.TryParse("ss14://example.com:5000", out var uri);

        Assert.That(ServerAddress.ApiAddress(uri).ToString(), Is.EqualTo("http://example.com:5000/"));
    }

    [Test]
    public void PathIsPreservedForServersBehindProxy()
    {
        ServerAddress.TryParse("ss14s://example.com/атмос", out var uri);

        Assert.That(
            ServerAddress.InfoAddress(uri).AbsolutePath,
            Does.EndWith("/info"),
            "the info URL lost the server path");
    }

    [Test]
    public void InfoAndStatusHangOffApiAddress()
    {
        ServerAddress.TryParse("ss14://example.com", out var uri);

        Assert.Multiple(() =>
        {
            Assert.That(ServerAddress.InfoAddress(uri).ToString(), Is.EqualTo("http://example.com:1212/info"));
            Assert.That(ServerAddress.StatusAddress(uri).ToString(), Is.EqualTo("http://example.com:1212/status"));
        });
    }
}
