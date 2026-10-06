using BetterAmongUs.Core.ModdedSupport;
using Xunit;

namespace HoryTweaks.Tests.ModdedSupport;

public class ModdedUserClassificationTests
{
    [Fact]
    public void ClassifiesHoryFlagAsHoryUser()
    {
        Assert.Equal(ModdedUserKind.HoryUser, ModdedUserClassification.Classify(isBetterUser: false, isHoryUser: true));
    }

    [Fact]
    public void ClassifiesBauHandshakeOnlyAsBetterUser()
    {
        Assert.Equal(ModdedUserKind.BetterUser, ModdedUserClassification.Classify(isBetterUser: true, isHoryUser: false));
    }

    [Fact]
    public void HoryFlagWinsOverBauHandshake()
    {
        // HoryTweaks peers complete the BAU handshake AND advertise, so both flags are set.
        Assert.Equal(ModdedUserKind.HoryUser, ModdedUserClassification.Classify(isBetterUser: true, isHoryUser: true));
    }

    [Fact]
    public void ClassifiesNoFlagsAsNone()
    {
        Assert.Equal(ModdedUserKind.None, ModdedUserClassification.Classify(isBetterUser: false, isHoryUser: false));
    }

    [Fact]
    public void HoryTweaksFlagHashMatchesGetFlagHash()
    {
        Assert.Equal(
            ModdedUserClassification.GetFlagHash("mod.horytweaks"),
            ModdedUserClassification.HoryTweaksFlagHash);
    }

    [Fact]
    public void IsHoryTweaksFlagHashAcceptsOnlyTheHoryFlag()
    {
        Assert.True(ModdedUserClassification.IsHoryTweaksFlagHash(ModdedUserClassification.HoryTweaksFlagHash));
        Assert.False(ModdedUserClassification.IsHoryTweaksFlagHash(0));
        Assert.False(ModdedUserClassification.IsHoryTweaksFlagHash(ModdedUserClassification.GetFlagHash("command.force.bau.prefix")));
    }

    [Fact]
    public void GetFlagHashIsDeterministicAndZeroForEmpty()
    {
        Assert.Equal(0, ModdedUserClassification.GetFlagHash(null));
        Assert.Equal(0, ModdedUserClassification.GetFlagHash(""));
        Assert.Equal(
            ModdedUserClassification.GetFlagHash("anticheat.disable.rpchandler="),
            ModdedUserClassification.GetFlagHash("anticheat.disable.rpchandler="));
    }
}
