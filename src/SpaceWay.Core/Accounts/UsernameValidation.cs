using Robust.Shared.AuthLib;

namespace SpaceWay.Core.Accounts;

/// <summary>
/// Player name validation.
/// </summary>
public static class UsernameValidation
{
    public static int MinLength => UsernameHelpers.NameLengthMin;

    public static int MaxLength => UsernameHelpers.NameLengthMax;

    public static UsernameProblem Check(string? username)
    {
        var trimmed = username?.Trim() ?? string.Empty;

        if (UsernameHelpers.IsNameValid(trimmed, out var reason))
            return UsernameProblem.None;

        return reason switch
        {
            UsernameHelpers.UsernameInvalidReason.Empty => UsernameProblem.Empty,
            UsernameHelpers.UsernameInvalidReason.TooShort => UsernameProblem.TooShort,
            UsernameHelpers.UsernameInvalidReason.TooLong => UsernameProblem.TooLong,
            _ => UsernameProblem.InvalidCharacters,
        };
    }

    public static bool IsValid(string? username) => Check(username) == UsernameProblem.None;
}

public enum UsernameProblem
{
    None,
    Empty,
    TooShort,
    TooLong,
    InvalidCharacters,
}
