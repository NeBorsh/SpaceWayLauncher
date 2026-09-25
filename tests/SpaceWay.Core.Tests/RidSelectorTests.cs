using NUnit.Framework;
using SpaceWay.Core.Engine;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class RidSelectorTests
{
    [Test]
    public void ExactMatchWins()
    {
        var best = RidSelector.FindBest(["linux-x64", "win-x64", "osx-x64"], "win-x64");

        Assert.That(best, Is.EqualTo("win-x64"));
    }

    [Test]
    public void MacOnArmFallsBackToX64()
    {
        var best = RidSelector.FindBest(["win-x64", "osx-x64"], "osx-arm64");

        Assert.That(best, Is.EqualTo("osx-x64"));
    }

    [Test]
    public void MacOnArmPrefersNativeBuild()
    {
        var best = RidSelector.FindBest(["osx-x64", "osx-arm64"], "osx-arm64");

        Assert.That(best, Is.EqualTo("osx-arm64"));
    }

    [Test]
    public void LinuxOnArmRefusesX64Build()
    {
        var best = RidSelector.FindBest(["linux-x64", "win-x64"], "linux-arm64");

        Assert.That(best, Is.Null);
    }

    [Test]
    public void NoBuildForPlatformReturnsNull()
    {
        var best = RidSelector.FindBest(["linux-x64"], "win-x64");

        Assert.That(best, Is.Null);
    }

    [Test]
    public void UnknownRidMatchesExactlyOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RidSelector.FindBest(["freebsd-x64"], "freebsd-x64"), Is.EqualTo("freebsd-x64"));
            Assert.That(RidSelector.FindBest(["linux-x64"], "freebsd-x64"), Is.Null);
        });
    }

    [Test]
    public void CurrentPlatformIsRecognized()
    {
        Assert.That(RidSelector.Current, Does.Not.Contain("unknown"));
    }
}
