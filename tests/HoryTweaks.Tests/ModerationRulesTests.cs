using BetterAmongUs.Modules.Moderation;
using Xunit;

namespace BetterAmongUs.Tests;

public class ModerationRulesTests
{
    private static ModerationContext Valid() => new(
        IsHost: true,
        IsInGame: true,
        IsEnded: false,
        IsStarting: false,
        TargetExists: true,
        TargetIsDummy: false,
        TargetIsLocal: false,
        TargetIsHost: false,
        TargetDataCollected: true);

    [Fact]
    public void AllowsHostActingOnReadyRemotePlayer()
    {
        Assert.Equal(ModerationDenial.None, ModerationRules.Evaluate(Valid()));
    }

    [Fact]
    public void RefusesWhenNotHostBeforeAnyOtherCheck()
    {
        var context = Valid() with { IsHost = false, TargetIsHost = true, IsEnded = true };
        Assert.Equal(ModerationDenial.NotHost, ModerationRules.Evaluate(context));
    }

    [Theory]
    [InlineData(nameof(ModerationContext.IsInGame), (int)ModerationDenial.NotInGame)]
    [InlineData(nameof(ModerationContext.IsEnded), (int)ModerationDenial.GameEnded)]
    [InlineData(nameof(ModerationContext.IsStarting), (int)ModerationDenial.GameStarting)]
    [InlineData(nameof(ModerationContext.TargetExists), (int)ModerationDenial.TargetMissing)]
    [InlineData(nameof(ModerationContext.TargetIsDummy), (int)ModerationDenial.TargetIsDummy)]
    [InlineData(nameof(ModerationContext.TargetIsLocal), (int)ModerationDenial.TargetIsSelf)]
    [InlineData(nameof(ModerationContext.TargetIsHost), (int)ModerationDenial.TargetIsHost)]
    [InlineData(nameof(ModerationContext.TargetDataCollected), (int)ModerationDenial.TargetNotReady)]
    public void RefusesEachUnsafeCondition(string flag, int expected)
    {
        var context = flag switch
        {
            nameof(ModerationContext.IsInGame) => Valid() with { IsInGame = false },
            nameof(ModerationContext.IsEnded) => Valid() with { IsEnded = true },
            nameof(ModerationContext.IsStarting) => Valid() with { IsStarting = true },
            nameof(ModerationContext.TargetExists) => Valid() with { TargetExists = false },
            nameof(ModerationContext.TargetIsDummy) => Valid() with { TargetIsDummy = true },
            nameof(ModerationContext.TargetIsLocal) => Valid() with { TargetIsLocal = true },
            nameof(ModerationContext.TargetIsHost) => Valid() with { TargetIsHost = true },
            _ => Valid() with { TargetDataCollected = false }
        };

        Assert.Equal((ModerationDenial)expected, ModerationRules.Evaluate(context));
    }
}
