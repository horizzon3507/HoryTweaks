using BetterAmongUs.Core.Moderation;
using Xunit;

namespace HoryTweaks.Tests.Moderation;

public class LevelKickRulesTests
{
    [Theory]
    [InlineData(9u)]  // one below threshold
    [InlineData(8u)]
    [InlineData(0u)]
    public void KicksPlayersBelowThreshold(uint level)
    {
        Assert.True(LevelKickRules.ShouldKick(level, kickLevelBelow: 10, minPlayers: 1, currentPlayers: 10, isLocalPlayer: false, kickEnabled: true));
    }

    [Theory]
    [InlineData(10u)] // exactly at threshold
    [InlineData(11u)] // one above threshold
    public void KeepsPlayersAtOrAboveThreshold(uint level)
    {
        Assert.False(LevelKickRules.ShouldKick(level, kickLevelBelow: 10, minPlayers: 1, currentPlayers: 10, isLocalPlayer: false, kickEnabled: true));
    }

    [Fact]
    public void NeverKicksLocalPlayer()
    {
        Assert.False(LevelKickRules.ShouldKick(0u, kickLevelBelow: 10, minPlayers: 1, currentPlayers: 10, isLocalPlayer: true, kickEnabled: true));
    }

    [Fact]
    public void DoesNothingWhenDisabled()
    {
        Assert.False(LevelKickRules.ShouldKick(0u, kickLevelBelow: 10, minPlayers: 1, currentPlayers: 10, isLocalPlayer: false, kickEnabled: false));
    }

    [Theory]
    [InlineData(1, 0)]   // minPlayers <= 1 never blocks, even with nobody else in the lobby
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(2, 2)]   // equal player count allows the kick
    [InlineData(5, 5)]
    [InlineData(5, 10)]
    public void MinimumPlayersGateAllowsAtOrBelowCount(int minPlayers, int currentPlayers)
    {
        Assert.True(LevelKickRules.ShouldKick(9u, kickLevelBelow: 10, minPlayers, currentPlayers, isLocalPlayer: false, kickEnabled: true));
    }

    [Theory]
    [InlineData(2, 1)]   // one player below the minimum blocks the kick
    [InlineData(5, 4)]
    [InlineData(10, 1)]
    public void MinimumPlayersGateBlocksWhenAboveCount(int minPlayers, int currentPlayers)
    {
        Assert.False(LevelKickRules.ShouldKick(9u, kickLevelBelow: 10, minPlayers, currentPlayers, isLocalPlayer: false, kickEnabled: true));
    }
}
