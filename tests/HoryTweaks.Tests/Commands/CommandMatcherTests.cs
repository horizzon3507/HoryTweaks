using BetterAmongUs.Core.Commands;
using Xunit;

namespace HoryTweaks.Tests.Commands;

public class CommandMatcherTests
{
    private static CommandDescriptor Command(string name, bool enabled = true, params string[] aliases)
        => new(name, [.. aliases, name], enabled);

    [Fact]
    public void ExactMatch_WinsOverPrefixMatch()
    {
        // "kill" is both an exact name and a prefix of "killer"; the exact command must win.
        CommandDescriptor[] commands =
        [
            Command("killer"),
            Command("kill"),
        ];

        Assert.Equal(1, CommandMatcher.Match("kill", commands));
    }

    [Fact]
    public void ExactMatch_OnAlias_WinsOverPrefixMatch()
    {
        CommandDescriptor[] commands =
        [
            Command("transferhost", aliases: ["th"]),
            Command("thing"),
        ];

        Assert.Equal(0, CommandMatcher.Match("th", commands));
    }

    [Fact]
    public void Match_IsCaseInsensitive()
    {
        CommandDescriptor[] commands = [Command("kick")];

        Assert.Equal(0, CommandMatcher.Match("KICK", commands));
        Assert.Equal(0, CommandMatcher.Match("Kick", commands));
        Assert.Equal(0, CommandMatcher.Match("Ki", commands));
    }

    [Fact]
    public void DisabledCommands_AreSkipped()
    {
        CommandDescriptor[] commands =
        [
            Command("kick", enabled: false),
            Command("killer"),
        ];

        // "kick" is disabled, so the prefix falls through to "killer".
        Assert.Equal(1, CommandMatcher.Match("k", commands));
        Assert.Equal(1, CommandMatcher.Match("ki", commands));
    }

    [Fact]
    public void AllDisabled_ReturnsMinusOne()
    {
        CommandDescriptor[] commands =
        [
            Command("kick", enabled: false),
            Command("kill", enabled: false),
        ];

        Assert.Equal(-1, CommandMatcher.Match("ki", commands));
    }

    [Fact]
    public void EmptyInput_MatchesAlphabeticallyFirstEnabledCommand()
    {
        // StartsWith("") is true for every name; the prefix pass orders by primary name.
        CommandDescriptor[] commands =
        [
            Command("kick"),
            Command("dump"),
            Command("help"),
        ];

        Assert.Equal(1, CommandMatcher.Match("", commands));
    }

    [Fact]
    public void EmptyInput_SkipsDisabledEvenWhenAlphabeticallyFirst()
    {
        CommandDescriptor[] commands =
        [
            Command("aaa", enabled: false),
            Command("bbb"),
        ];

        Assert.Equal(1, CommandMatcher.Match("", commands));
    }

    [Fact]
    public void NoMatch_ReturnsMinusOne()
    {
        CommandDescriptor[] commands =
        [
            Command("kick"),
            Command("dump"),
        ];

        Assert.Equal(-1, CommandMatcher.Match("xyz", commands));
    }

    [Fact]
    public void PrefixMatch_PicksAlphabeticallyFirstName()
    {
        CommandDescriptor[] commands =
        [
            Command("kill"),
            Command("kick"),
            Command("king"),
        ];

        Assert.Equal(1, CommandMatcher.Match("k", commands));
        Assert.Equal(1, CommandMatcher.Match("ki", commands));
    }

    [Fact]
    public void PrefixMatch_MatchesAliasesToo()
    {
        CommandDescriptor[] commands =
        [
            Command("transferhost", aliases: ["th"]),
            Command("dump"),
        ];

        Assert.Equal(0, CommandMatcher.Match("tr", commands));
        Assert.Equal(0, CommandMatcher.Match("t", commands));
    }

    [Fact]
    public void PrefixMatch_PrefersPrimaryNameOrderingNotAliasOrdering()
    {
        // "beta" sorts before "alpha" alphabetically by primary name even though
        // "alpha" owns the alias "zz" that also matches the prefix.
        CommandDescriptor[] commands =
        [
            Command("alpha", aliases: ["zz"]),
            Command("beta"),
        ];

        Assert.Equal(1, CommandMatcher.Match("b", commands));
    }

    [Fact]
    public void EmptyCommandList_ReturnsMinusOne()
    {
        Assert.Equal(-1, CommandMatcher.Match("kick", []));
    }
}
