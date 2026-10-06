using HoryTweaks.Core.Moderation;
using Xunit;

namespace HoryTweaks.Tests.Moderation;

public class HostPlayerChecksTests
{
    private static bool CanKick(
        bool isHost = true,
        bool isLocalPlayer = false,
        bool dataCollected = true,
        bool bypassDataCheck = false,
        bool isHostPlayer = false,
        bool isDummy = false,
        bool ban = false,
        bool forceBan = false)
    {
        return HostPlayerChecks.CanKick(isHost, isLocalPlayer, dataCollected, bypassDataCheck, isHostPlayer, isDummy, ban, forceBan, out _lastShouldBan);
    }

    private static bool _lastShouldBan;

    [Fact]
    public void HostCanKickCollectedRemotePlayer()
    {
        Assert.True(CanKick());
        Assert.False(_lastShouldBan);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldBanIsBanOrForceBan(bool ban, bool forceBan, bool expected)
    {
        Assert.True(CanKick(ban: ban, forceBan: forceBan));
        Assert.Equal(expected, _lastShouldBan);
    }

    [Fact]
    public void RefusesWhenNotHost()
    {
        Assert.False(CanKick(isHost: false));
    }

    [Fact]
    public void RefusesLocalPlayer()
    {
        Assert.False(CanKick(isLocalPlayer: true));
    }

    [Fact]
    public void RefusesHostPlayer()
    {
        Assert.False(CanKick(isHostPlayer: true));
    }

    [Fact]
    public void RefusesDummy()
    {
        Assert.False(CanKick(isDummy: true));
    }

    [Fact]
    public void RefusesUncollectedDataWithoutBypass()
    {
        Assert.False(CanKick(dataCollected: false));
    }

    [Fact]
    public void BypassDataCheckAllowsUncollectedData()
    {
        Assert.True(CanKick(dataCollected: false, bypassDataCheck: true));
    }

    [Fact]
    public void ShouldBanStillComputedWhenRefused()
    {
        Assert.False(CanKick(isHost: false, ban: true));
        Assert.True(_lastShouldBan);
    }
}
