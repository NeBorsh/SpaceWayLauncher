using NUnit.Framework;
using SpaceWay.Core.Accounts;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Rules come from the game library. The tests pin down not the rules themselves
/// but that the launcher applies them: a name it accepts must be accepted by the server.
/// </summary>
[TestFixture]
public sealed class UsernameValidationTests
{
    [Test]
    [TestCase("Borsh_")]
    [TestCase("Player123")]
    [TestCase("abc")]
    public void ValidName_Passes(string username)
    {
        Assert.That(UsernameValidation.IsValid(username), Is.True);
    }

    [Test]
    public void NameWithSpace_IsRejected()
    {
        Assert.That(UsernameValidation.Check("с пробелом"),
            Is.EqualTo(UsernameProblem.InvalidCharacters));
    }

    [Test]
    public void CyrillicName_IsRejected()
    {
        Assert.That(UsernameValidation.Check("Игрок"),
            Is.EqualTo(UsernameProblem.InvalidCharacters));
    }

    [Test]
    public void TooShortName_IsRejected()
    {
        Assert.That(UsernameValidation.Check("ab"), Is.EqualTo(UsernameProblem.TooShort));
    }

    [Test]
    public void TooLongName_IsRejected()
    {
        var name = new string('a', UsernameValidation.MaxLength + 1);

        Assert.That(UsernameValidation.Check(name), Is.EqualTo(UsernameProblem.TooLong));
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void EmptyName_DiffersFromInputError(string? username)
    {
        Assert.That(UsernameValidation.Check(username), Is.EqualTo(UsernameProblem.Empty));
    }

    [Test]
    public void SurroundingSpaces_AreTrimmed()
    {
        Assert.That(UsernameValidation.IsValid("  Borsh_  "), Is.True);
    }
}
