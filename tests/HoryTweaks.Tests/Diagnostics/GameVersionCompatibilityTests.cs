using BetterAmongUs.Core.Diagnostics;
using Xunit;
using BetterAmongUs.Core.Diagnostics;

namespace HoryTweaks.Tests.Diagnostics;

public class GameVersionCompatibilityTests
{
    private static readonly string[] Supported = ["2026.9.29"];

    [Theory]
    [InlineData("2026.9.29", nameof(GameVersionStatus.Supported))]
    [InlineData("v2026.9.29s", nameof(GameVersionStatus.Supported))]
    [InlineData("2026.10.1", nameof(GameVersionStatus.Newer))]
    [InlineData("2027.1.1", nameof(GameVersionStatus.Newer))]
    [InlineData("2026.6.10", nameof(GameVersionStatus.Older))]
    [InlineData("2025.12.31", nameof(GameVersionStatus.Older))]
    [InlineData("", nameof(GameVersionStatus.Unknown))]
    [InlineData(null, nameof(GameVersionStatus.Unknown))]
    [InlineData("unknown", nameof(GameVersionStatus.Unknown))]
    public void Compare_ClassifiesRunningVersion(string? current, string expected)
    {
        Assert.Equal(Enum.Parse<GameVersionStatus>(expected), GameVersionCompatibility.Compare(current, Supported));
    }

    [Fact]
    public void Compare_UsesRangeWhenSeveralVersionsAreSupported()
    {
        string[] supported = ["2026.9.29", "2026.6.10", "2026.8.1"];

        Assert.Equal(GameVersionStatus.Supported, GameVersionCompatibility.Compare("2026.6.10", supported));
        Assert.Equal(GameVersionStatus.Supported, GameVersionCompatibility.Compare("2026.7.1", supported));
        Assert.Equal(GameVersionStatus.Supported, GameVersionCompatibility.Compare("2026.9.29", supported));
        Assert.Equal(GameVersionStatus.Older, GameVersionCompatibility.Compare("2026.6.9", supported));
        Assert.Equal(GameVersionStatus.Newer, GameVersionCompatibility.Compare("2026.9.30", supported));
    }

    [Fact]
    public void Compare_ReturnsUnknownWhenNoSupportedVersionParses()
    {
        Assert.Equal(GameVersionStatus.Unknown, GameVersionCompatibility.Compare("2026.9.29", ["latest"]));
        Assert.Equal(GameVersionStatus.Unknown, GameVersionCompatibility.Compare("2026.9.29", []));
    }

    [Fact]
    public void FormatSupportedRange_ShowsSingleVersionOrOrderedRange()
    {
        Assert.Equal("2026.9.29", GameVersionCompatibility.FormatSupportedRange(Supported));
        Assert.Equal("2026.6.10 - 2026.9.29", GameVersionCompatibility.FormatSupportedRange(["2026.9.29", "2026.6.10"]));
    }
}
