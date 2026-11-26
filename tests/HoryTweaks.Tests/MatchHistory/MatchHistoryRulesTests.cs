using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class MatchHistoryRulesTests
{
    [Theory]
    [InlineData((int)ClientGameState.NotJoined, true)]
    [InlineData((int)ClientGameState.Joined, true)]
    [InlineData((int)ClientGameState.Ended, true)]
    [InlineData((int)ClientGameState.Started, false)]
    public void CanView_denies_running_matches(int state, bool expected)
    {
        Assert.Equal(expected, MatchHistoryRules.CanView((ClientGameState)state));
    }

    [Fact]
    public void CanView_denies_missing_client()
    {
        Assert.False(MatchHistoryRules.CanView(null));
    }

    [Fact]
    public void ShouldBeginRecording_requires_every_condition()
    {
        Assert.True(MatchHistoryRules.ShouldBeginRecording(true, true, false, ClientGameState.Started, true, false));

        Assert.False(MatchHistoryRules.ShouldBeginRecording(false, true, false, ClientGameState.Started, true, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, false, false, ClientGameState.Started, true, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, true, true, ClientGameState.Started, true, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, true, false, ClientGameState.Joined, true, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, true, false, ClientGameState.Ended, true, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, true, false, null, true, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, true, false, ClientGameState.Started, false, false));
        Assert.False(MatchHistoryRules.ShouldBeginRecording(true, true, false, ClientGameState.Started, true, true));
    }

    [Fact]
    public void ShouldBeginRecording_passes_for_exactly_one_combination()
    {
        var bools = new[] { false, true };
        ClientGameState?[] states = [ClientGameState.NotJoined, ClientGameState.Joined, ClientGameState.Started, ClientGameState.Ended, (ClientGameState)99, null];

        var passing =
            from enabled in bools
            from armed in bools
            from freePlay in bools
            from state in states
            from shipPresent in bools
            from introPlaying in bools
            where MatchHistoryRules.ShouldBeginRecording(enabled, armed, freePlay, state, shipPresent, introPlaying)
            select (enabled, armed, freePlay, state, shipPresent, introPlaying);

        Assert.Equal(
            [(true, true, false, (ClientGameState?)ClientGameState.Started, true, false)],
            passing.ToList());
    }
}
