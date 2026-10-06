using BetterAmongUs.Core.Formatting;
using Xunit;

namespace HoryTweaks.Tests.Formatting;

public class ChatFormatTests
{
    [Fact]
    public void StripRichTextRemovesColorAndSizeTags()
    {
        string input = "<color=#ffffbe>Name</color> hello <size=75%>small</size>";

        Assert.Equal("Name hello small", ChatFormat.StripRichText(input));
    }

    [Fact]
    public void StripRichTextRemovesBoldAndClosingTags()
    {
        string input = "<b>(<color=#00ff44>System Message</color>)</b>";

        Assert.Equal("(System Message)", ChatFormat.StripRichText(input));
    }

    [Fact]
    public void StripRichTextKeepsNonTagAngleBrackets()
    {
        Assert.Equal("i <3 among us and 1 < 2 > 0", ChatFormat.StripRichText("i <3 among us and 1 < 2 > 0"));
    }

    [Fact]
    public void StripRichTextKeepsUnclosedTagText()
    {
        Assert.Equal("text <color", ChatFormat.StripRichText("text <color"));
    }

    [Fact]
    public void StripRichTextHandlesEmptyAndNull()
    {
        Assert.Equal(string.Empty, ChatFormat.StripRichText(""));
        Assert.Equal(string.Empty, ChatFormat.StripRichText(null));
    }

    [Fact]
    public void StripRichTextDoesNotFailOnNestedBracket()
    {
        // "<b<c" is not a tag, but the "<c>" inside it is parsed as one
        Assert.Equal("a <bd", ChatFormat.StripRichText("a <b<c>d"));
    }
}
