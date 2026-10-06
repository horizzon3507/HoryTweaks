using System.Text.RegularExpressions;

namespace BetterAmongUs.Core.Validation;

/// <summary>
/// Reasons a friend code fails validation.
/// </summary>
internal enum FriendCodeInvalidReason
{
    /// The friend code passes all checks.
    None,

    /// The friend code is null or empty.
    Empty,

    /// The friend code contains characters outside letters, digits and '#',
    /// or contains spaces.
    InvalidChars,

    /// The friend code does not end with '#' followed by exactly 4 digits.
    MissingTag,

    /// The name part before the tag is shorter than 5 characters.
    NameTooShort,

    /// The name part before the tag is longer than 10 characters.
    NameTooLong
}

/// <summary>
/// Unity-independent friend code validation used by the player info display
/// and by the host's invalid-friend-code kick rule.
/// </summary>
internal static class FriendCodeRules
{
    private static readonly Regex _charsetPattern = new(@"^[a-zA-Z0-9#]+$", RegexOptions.Compiled);

    /// <summary>
    /// Classifies a friend code. The checks mirror the in-game display logic:
    /// charset first, then a trailing '#dddd' tag, then a 5-10 character name part.
    /// </summary>
    internal static FriendCodeInvalidReason Validate(string? friendCode)
    {
        if (string.IsNullOrEmpty(friendCode))
            return FriendCodeInvalidReason.Empty;

        // Basic pattern: alphanumeric and '#' only
        if (!_charsetPattern.IsMatch(friendCode) || friendCode.Contains(' '))
            return FriendCodeInvalidReason.InvalidChars;

        // Must end with '#' followed by exactly 4 digits
        if (!Regex.IsMatch(friendCode, @"#\d{4}$"))
            return FriendCodeInvalidReason.MissingTag;

        // The part before the '#' should be a reasonable length
        int nameLength = friendCode.Length - 5;
        if (nameLength < 5)
            return FriendCodeInvalidReason.NameTooShort;
        if (nameLength > 10)
            return FriendCodeInvalidReason.NameTooLong;

        return FriendCodeInvalidReason.None;
    }
}
