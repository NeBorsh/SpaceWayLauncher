using NUnit.Framework;
using SpaceWay.Core.Hubs;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class ServerAddressTests
{
    [Test]
    [TestCase("ss14://example.com:1212/", "ss14://example.com:1212")]
    [TestCase("ss14://example.com:1212", "ss14://example.com:1212")]
    [TestCase("ss14://EXAMPLE.com:1212", "ss14://example.com:1212")]
    [TestCase("  ss14://example.com:1212  ", "ss14://example.com:1212")]
    public void DifferentSpellings_CollapseIntoOne(string input, string expected)
    {
        Assert.That(ServerAddress.Normalize(input), Is.EqualTo(expected));
    }

    [Test]
    public void DefaultPort_IsSpelledOut()
    {
        Assert.That(
            ServerAddress.Normalize("ss14://example.com"),
            Is.EqualTo(ServerAddress.Normalize("ss14://example.com:1212")));
    }

    [Test]
    public void CustomPort_IsKept()
    {
        Assert.That(
            ServerAddress.Normalize("ss14://example.com:1213"),
            Is.Not.EqualTo(ServerAddress.Normalize("ss14://example.com:1212")));
    }

    [Test]
    public void UnparsableAddress_IsNotLost()
    {
        Assert.That(ServerAddress.Normalize("совсем не адрес"), Is.EqualTo("совсем не адрес"));
    }
}
