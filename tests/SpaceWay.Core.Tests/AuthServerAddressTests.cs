using NUnit.Framework;
using SpaceWay.Core.Accounts;

namespace SpaceWay.Core.Tests;

[TestFixture]
public sealed class AuthServerAddressTests
{
    [Test]
    [TestCase("auth.example.com", "https://auth.example.com/")]
    [TestCase("https://auth.example.com", "https://auth.example.com/")]
    [TestCase("  https://auth.example.com/api/  ", "https://auth.example.com/api/")]
    [TestCase("http://localhost:5000", "http://localhost:5000/")]
    [TestCase("http://127.0.0.1:5000", "http://127.0.0.1:5000/")]
    public void ValidAddress_IsNormalized(string input, string expected)
    {
        var problem = AuthServer.ParseAddress(input, out var address);

        Assert.That(problem, Is.EqualTo(AuthServerAddressProblem.None));
        Assert.That(address?.AbsoluteUri, Is.EqualTo(expected));
    }

    [Test]
    public void PlainHttpToRemoteHost_IsRejected()
    {
        var problem = AuthServer.ParseAddress("http://auth.example.com", out var address);

        Assert.That(problem, Is.EqualTo(AuthServerAddressProblem.Insecure));
        Assert.That(address, Is.Null);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankAddress_IsEmpty(string input)
    {
        Assert.That(AuthServer.ParseAddress(input, out _), Is.EqualTo(AuthServerAddressProblem.Empty));
    }

    [Test]
    [TestCase("ftp://auth.example.com")]
    [TestCase("https://")]
    [TestCase("not an address")]
    public void Garbage_IsInvalid(string input)
    {
        Assert.That(AuthServer.ParseAddress(input, out _), Is.EqualTo(AuthServerAddressProblem.Invalid));
    }
}
