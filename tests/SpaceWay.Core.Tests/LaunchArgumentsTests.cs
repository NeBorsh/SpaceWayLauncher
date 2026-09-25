using NUnit.Framework;
using SpaceWay.Core.Connecting;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class LaunchArgumentsTests
{
    [Test]
    public void Redial_GivesServer()
    {
        var target = LaunchArguments.ConnectTarget(["--connect", "ss14://example.com:1212/", "--reason", "Restart"]);

        Assert.That(target?.AbsoluteUri, Is.EqualTo("ss14://example.com:1212/"));
    }

    [Test]
    [TestCase("ss14://example.com")]
    [TestCase("ss14s://example.com/core")]
    [TestCase("SS14://example.com:1212/")]
    public void Link_GivesServer(string link)
    {
        Assert.That(LaunchArguments.ConnectTarget([link]), Is.Not.Null);
    }

    [Test]
    public void NoArguments_GiveNothing()
    {
        Assert.That(LaunchArguments.ConnectTarget([]), Is.Null);
    }

    [Test]
    [TestCase("--connect")]
    [TestCase("https://example.com")]
    [TestCase("ss14://")]
    public void Garbage_GivesNothing(string arg)
    {
        Assert.That(LaunchArguments.ConnectTarget([arg]), Is.Null);
    }

    [Test]
    public void ConnectWithoutServer_GivesNothing()
    {
        Assert.That(LaunchArguments.ConnectTarget(["--connect", "https://example.com"]), Is.Null);
    }
}
