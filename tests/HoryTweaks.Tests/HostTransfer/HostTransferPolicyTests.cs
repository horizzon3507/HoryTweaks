using HoryTweaks.Core.HostTransfer;
using Xunit;
using HoryTweaks.Core.HostTransfer;

namespace HoryTweaks.Tests.HostTransfer;

public class HostTransferPolicyTests
{
    private static HostTransferContext Valid() => new(
        IsHost: true,
        IsInGame: true,
        IsEnded: false,
        IsStarting: false,
        IsLobby: false,
        RequireLobby: false,
        TargetExists: true,
        TargetIsDummy: false,
        TargetIsLocal: false,
        TargetIsHost: false,
        TargetDataCollected: true);

    private static HostTransferContext ValidLobby() => Valid() with { IsLobby = true, RequireLobby = true };

    [Fact]
    public void AllowsHostTransferToReadyRemotePlayer()
    {
        Assert.Equal(HostTransferDenial.None, HostTransferPolicy.Evaluate(Valid()));
    }

    [Fact]
    public void AllowsAfkTransferInsideLobby()
    {
        Assert.Equal(HostTransferDenial.None, HostTransferPolicy.Evaluate(ValidLobby()));
    }

    [Fact]
    public void RefusesWhenNotHostBeforeAnyOtherCheck()
    {
        var context = Valid() with { IsHost = false, IsEnded = true, TargetIsHost = true };
        Assert.Equal(HostTransferDenial.NotHost, HostTransferPolicy.Evaluate(context));
    }

    [Theory]
    [InlineData(nameof(HostTransferContext.IsInGame), (int)HostTransferDenial.NotInGame)]
    [InlineData(nameof(HostTransferContext.IsEnded), (int)HostTransferDenial.GameEnded)]
    [InlineData(nameof(HostTransferContext.IsStarting), (int)HostTransferDenial.GameStarting)]
    [InlineData(nameof(HostTransferContext.TargetExists), (int)HostTransferDenial.TargetMissing)]
    [InlineData(nameof(HostTransferContext.TargetIsDummy), (int)HostTransferDenial.TargetIsDummy)]
    [InlineData(nameof(HostTransferContext.TargetIsLocal), (int)HostTransferDenial.TargetIsLocal)]
    [InlineData(nameof(HostTransferContext.TargetIsHost), (int)HostTransferDenial.TargetIsHost)]
    [InlineData(nameof(HostTransferContext.TargetDataCollected), (int)HostTransferDenial.TargetNotReady)]
    public void RefusesEachUnsafeCondition(string flag, int expected)
    {
        var context = flag switch
        {
            nameof(HostTransferContext.IsInGame) => Valid() with { IsInGame = false },
            nameof(HostTransferContext.IsEnded) => Valid() with { IsEnded = true },
            nameof(HostTransferContext.IsStarting) => Valid() with { IsStarting = true },
            nameof(HostTransferContext.TargetExists) => Valid() with { TargetExists = false },
            nameof(HostTransferContext.TargetIsDummy) => Valid() with { TargetIsDummy = true },
            nameof(HostTransferContext.TargetIsLocal) => Valid() with { TargetIsLocal = true },
            nameof(HostTransferContext.TargetIsHost) => Valid() with { TargetIsHost = true },
            _ => Valid() with { TargetDataCollected = false }
        };

        Assert.Equal((HostTransferDenial)expected, HostTransferPolicy.Evaluate(context));
    }

    [Fact]
    public void RefusesAfkTransferOutsideLobbyOnlyWhenRequired()
    {
        var context = Valid() with { RequireLobby = true };
        Assert.Equal(HostTransferDenial.NotInLobby, HostTransferPolicy.Evaluate(context));

        Assert.Equal(HostTransferDenial.None, HostTransferPolicy.Evaluate(Valid()));
    }

    [Fact]
    public void RefusesAfkTransferOutsideLobbyBeforeCheckingTarget()
    {
        var context = Valid() with { RequireLobby = true, TargetExists = false };
        Assert.Equal(HostTransferDenial.NotInLobby, HostTransferPolicy.Evaluate(context));
    }

    [Fact]
    public void PicksLowestClientIdAsLongestTenured()
    {
        var candidates = new[]
        {
            new HostTransferCandidate(7, false, false, false, true),
            new HostTransferCandidate(3, false, false, false, true),
            new HostTransferCandidate(9, false, false, false, true),
        };

        Assert.Equal(3, HostTransferPolicy.PickSuccessorClientId(candidates));
    }

    [Fact]
    public void SkipsIneligibleCandidates()
    {
        var candidates = new[]
        {
            new HostTransferCandidate(1, false, false, true, true),   // already host
            new HostTransferCandidate(2, false, true, false, true),   // local player
            new HostTransferCandidate(3, true, false, false, true),   // dummy
            new HostTransferCandidate(4, false, false, false, false), // not ready
            new HostTransferCandidate(-1, false, false, false, true), // no client id
            new HostTransferCandidate(8, false, false, false, true),
        };

        Assert.Equal(8, HostTransferPolicy.PickSuccessorClientId(candidates));
    }

    [Fact]
    public void ReturnsNoEligibleIdWhenNobodyQualifies()
    {
        var candidates = new[]
        {
            new HostTransferCandidate(1, false, true, false, true),
            new HostTransferCandidate(2, true, false, false, true),
        };

        Assert.Equal(-1, HostTransferPolicy.PickSuccessorClientId(candidates));
        Assert.Equal(-1, HostTransferPolicy.PickSuccessorClientId([]));
    }

    [Theory]
    [InlineData(59.9, 1, false)]
    [InlineData(60.0, 1, true)]
    [InlineData(300.0, 5, true)]
    [InlineData(299.9, 5, false)]
    [InlineData(600.0, 0, false)]
    [InlineData(600.0, -5, false)]
    public void AfkTransferFiresAtConfiguredBoundary(double idleSeconds, int afkMinutes, bool expected)
    {
        Assert.Equal(expected, HostTransferPolicy.ShouldTransferOnAfk(idleSeconds, afkMinutes));
    }
}
