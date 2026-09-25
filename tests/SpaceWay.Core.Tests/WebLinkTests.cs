using NUnit.Framework;
using SpaceWay.Core.Util;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class WebLinkTests
{
    [TestCase("https://discord.gg/example")]
    [TestCase("http://wiki.example/Главная")]
    public void AcceptsWebsites(string link)
    {
        Assert.That(WebLink.TryParse(link, out _), Is.True);
    }

    [TestCase("file:///C:/Windows/System32/calc.exe")]
    [TestCase(@"C:\Windows\System32\calc.exe")]
    [TestCase("javascript:alert(1)")]
    [TestCase("ms-settings:privacy")]
    [TestCase("ss14://game.example")]
    [TestCase("wiki.example")]
    [TestCase("")]
    [TestCase(null)]
    public void RejectsEverythingElse(string? link)
    {
        Assert.That(WebLink.TryParse(link, out _), Is.False);
    }
}
