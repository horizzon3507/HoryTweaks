using HoryTweaks.Core.Presets;
using System.Text.RegularExpressions;
using Xunit;
using HoryTweaks.Core.Moderation;

namespace HoryTweaks.Tests.Moderation;

public class BanListMigrationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("// Example banned player names", false)]
    [InlineData("# comment", false)]
    [InlineData("HackerPlayer123", true)]
    [InlineData("**Bot**", true)]
    public void IsPatternLine_skips_blank_lines_and_comments(string line, bool expected)
    {
        Assert.Equal(expected, BanListMigration.IsPatternLine(line));
    }

    [Theory]
    [InlineData("**Bot**", true)]
    [InlineData("Exploit**", true)]
    [InlineData("^TNT$", false)]
    [InlineData("a*b", false)]
    public void UsesWildcard_detects_the_legacy_double_star(string line, bool expected)
    {
        Assert.Equal(expected, BanListMigration.UsesWildcard(line));
    }

    [Theory]
    [InlineData("HackerPlayer123", "(?i)^HackerPlayer123$")]
    [InlineData("**Bot**", "(?i)Bot")]
    [InlineData("**Script", "(?i)Script$")]
    [InlineData("Exploit**", "(?i)^Exploit")]
    [InlineData("a.b", @"(?i)^a\.b$")]
    public void NameWildcardToRegex_anchors_by_wildcard_position(string legacy, string expected)
    {
        Assert.Equal(expected, BanListMigration.NameWildcardToRegex(legacy));
    }

    [Theory]
    [InlineData("HackerPlayer123", "hackerplayer123", true)]
    [InlineData("HackerPlayer123", "HackerPlayer1234", false)]
    [InlineData("**Bot**", "RoBoTic", true)]
    [InlineData("**Bot**", "Robert", false)]
    [InlineData("**Script", "JavaScript", true)]
    [InlineData("**Script", "Scripter", false)]
    [InlineData("Exploit**", "Exploiter", true)]
    [InlineData("Exploit**", "MyExploit", false)]
    [InlineData("a.b", "A.B", true)]
    [InlineData("a.b", "aXb", false)]
    public void Converted_name_patterns_keep_the_legacy_wildcard_semantics(string legacy, string playerName, bool expected)
    {
        var pattern = BanListMigration.NameWildcardToRegex(legacy);

        Assert.Equal(expected, Regex.IsMatch(playerName, pattern));
    }

    [Theory]
    [InlineData("start", "(?i)(?: |^)start(?: |$)")]
    [InlineData("**bot**", "(?i)(?: |^).*bot.*(?: |$)")]
    [InlineData("(free)", @"(?i)(?: |^)\(free\)(?: |$)")]
    public void ChatWildcardToRegex_bounds_the_word_and_escapes_metacharacters(string legacy, string expected)
    {
        Assert.Equal(expected, BanListMigration.ChatWildcardToRegex(legacy));
    }

    [Theory]
    [InlineData("start", "please START now", true)]
    [InlineData("start", "start", true)]
    [InlineData("start", "we are starting", false)]
    [InlineData("start", "restart", false)]
    [InlineData("**bot**", "robots rule", true)]
    [InlineData("**bot**", "hello there", false)]
    [InlineData("(free)", "(free) skins", true)]
    [InlineData("(free)", "free skins", false)]
    public void Converted_chat_patterns_keep_the_legacy_wildcard_semantics(string legacy, string message, bool expected)
    {
        var pattern = BanListMigration.ChatWildcardToRegex(legacy);

        Assert.Equal(expected, Regex.IsMatch(message, pattern));
    }
}
