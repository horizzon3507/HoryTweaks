using HoryTweaks.Core.Validation;
using Xunit;

namespace HoryTweaks.Tests.Validation;

public class FriendCodeRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyInputReturnsEmpty(string? code)
    {
        Assert.Equal(FriendCodeInvalidReason.Empty, FriendCodeRules.Validate(code));
    }

    [Theory]
    [InlineData("abcde#1234")]   // minimum name length, standard tag
    [InlineData("abcdefghij#1234")] // maximum name length
    [InlineData("ab#cd#1234")]   // '#' inside the name part is accepted today
    [InlineData("abcde#1234\n")] // trailing newline still matches ($ anchors before \n)
    public void ValidFriendCodesReturnNone(string code)
    {
        Assert.Equal(FriendCodeInvalidReason.None, FriendCodeRules.Validate(code));
    }

    [Theory]
    [InlineData("abc de#1234")]  // space is rejected by the charset check
    [InlineData("abcde#1234 ")]  // trailing space
    [InlineData("abcde!#1234")]  // punctuation outside the allowed charset
    [InlineData(" ")]
    public void InvalidCharactersReturnInvalidChars(string code)
    {
        Assert.Equal(FriendCodeInvalidReason.InvalidChars, FriendCodeRules.Validate(code));
    }

    [Theory]
    [InlineData("abcde#123")]    // only 3 digits
    [InlineData("abcde#12345")]  // 5 digits: '#dddd$' no longer anchors the last 4
    [InlineData("abcde1234")]    // no '#'
    [InlineData("abcde#12a4")]   // non-digit inside the tag
    public void MissingOrMalformedTagReturnsMissingTag(string code)
    {
        Assert.Equal(FriendCodeInvalidReason.MissingTag, FriendCodeRules.Validate(code));
    }

    [Theory]
    [InlineData("abcd#1234")]  // name part is 4 chars
    [InlineData("#1234")]      // empty name part
    public void ShortNameReturnsNameTooShort(string code)
    {
        Assert.Equal(FriendCodeInvalidReason.NameTooShort, FriendCodeRules.Validate(code));
    }

    [Fact]
    public void LongNameReturnsNameTooLong()
    {
        Assert.Equal(FriendCodeInvalidReason.NameTooLong, FriendCodeRules.Validate("abcdefghijk#1234"));
    }
}
