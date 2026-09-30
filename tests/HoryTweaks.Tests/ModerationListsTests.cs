using BetterAmongUs.Modules.Moderation;
using Xunit;

namespace BetterAmongUs.Tests;

public class ModerationListsTests
{
    [Fact]
    public void CountEntriesIgnoresBlankAndCommentLines()
    {
        string[] lines =
        [
            "# player bans",
            "",
            "   ",
            "// legacy comment",
            "ABCD#1234,0123456789abcdef",
            "  ^bad.*name$  ",
        ];

        Assert.Equal(2, ModerationLists.CountEntries(lines));
    }

    [Fact]
    public void CountEntriesIsZeroForEmptyFile()
    {
        Assert.Equal(0, ModerationLists.CountEntries([]));
    }
}
