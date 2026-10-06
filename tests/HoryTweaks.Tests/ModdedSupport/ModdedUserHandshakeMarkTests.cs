using HoryTweaks.Core.ModdedSupport;
using Xunit;

namespace HoryTweaks.Tests.ModdedSupport;

public class ModdedUserHandshakeMarkTests
{
    [Fact]
    public void PayloadWithHandshakeMarkClassifiesAsHoryUser()
    {
        const int tempKey = 123456;
        int mark = ModdedUserClassification.GetHoryTweaksHandshakeMark(tempKey);

        bool advertisedHory = ModdedUserClassification.IsHoryTweaksHandshakeMark(tempKey, mark);

        Assert.Equal(ModdedUserKind.HoryUser, ModdedUserClassification.Classify(isBetterUser: true, advertisedHory));
    }

    [Fact]
    public void PayloadWithoutHandshakeMarkClassifiesAsBetterUser()
    {
        // Upstream BAU writes no trailing field, so no mark is read at all.
        Assert.Equal(ModdedUserKind.BetterUser, ModdedUserClassification.Classify(isBetterUser: true, isHoryUser: false));
    }

    [Fact]
    public void NoHandshakeClassifiesAsNone()
    {
        Assert.Equal(ModdedUserKind.None, ModdedUserClassification.Classify(isBetterUser: false, isHoryUser: false));
    }

    [Fact]
    public void MarkIsBoundToItsOwnTempKey()
    {
        // A mark copied verbatim into another handshake's payload must not validate.
        int mark = ModdedUserClassification.GetHoryTweaksHandshakeMark(tempKey: 111);
        Assert.False(ModdedUserClassification.IsHoryTweaksHandshakeMark(tempKey: 222, mark));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void MalformedMarkIsRejected(int mark)
    {
        Assert.False(ModdedUserClassification.IsHoryTweaksHandshakeMark(tempKey: 42, mark));
    }

    [Fact]
    public void RawHoryFlagHashIsNotAcceptedAsMark()
    {
        // The bare flag hash is not a handshake mark: the mark must be derived from the
        // payload's own tempKey.
        Assert.False(ModdedUserClassification.IsHoryTweaksHandshakeMark(
            tempKey: 42, ModdedUserClassification.HoryTweaksFlagHash));
    }

    [Fact]
    public void OtherFlagHashesAreNotAcceptedAsMark()
    {
        int otherMark = ModdedUserClassification.GetFlagHash("command.force.bau.prefix");
        Assert.False(ModdedUserClassification.IsHoryTweaksHandshakeMark(tempKey: 42, otherMark));
    }

    [Fact]
    public void HandshakeMarkIsDeterministicAndVariesByTempKey()
    {
        Assert.Equal(
            ModdedUserClassification.GetHoryTweaksHandshakeMark(7),
            ModdedUserClassification.GetHoryTweaksHandshakeMark(7));
        Assert.NotEqual(
            ModdedUserClassification.GetHoryTweaksHandshakeMark(7),
            ModdedUserClassification.GetHoryTweaksHandshakeMark(8));
    }

    [Fact]
    public void HandshakeMarkSupportsNegativeTempKeys()
    {
        int mark = ModdedUserClassification.GetHoryTweaksHandshakeMark(-999);
        Assert.True(ModdedUserClassification.IsHoryTweaksHandshakeMark(-999, mark));
    }
}
