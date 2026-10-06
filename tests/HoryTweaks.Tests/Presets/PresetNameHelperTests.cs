using BetterAmongUs.Core.Presets;
using Xunit;

namespace HoryTweaks.Tests.Presets;

public class PresetNameHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<color=red></color>")]
    [InlineData("<b> </b>")]
    public void Normalize_ReturnsNullWhenNothingUsableRemains(string? raw)
    {
        Assert.Null(PresetNameHelper.Normalize(raw));
    }

    [Fact]
    public void Normalize_StripsRichTextAndCollapsesWhitespace()
    {
        Assert.Equal("Sunday Lobby", PresetNameHelper.Normalize("  <color=#FF0000>Sunday</color>\t\n  Lobby  "));
    }

    [Fact]
    public void Normalize_ClampsToMaxLengthWithoutTrailingSpace()
    {
        string raw = "Twelve chars ok" + " overflow beyond limit";

        string? normalized = PresetNameHelper.Normalize(raw);

        Assert.NotNull(normalized);
        Assert.True(normalized.Length <= PresetNameHelper.MaxLength);
        Assert.Equal(normalized.TrimEnd(), normalized);
    }

    [Fact]
    public void Normalize_KeepsPlainNamesUnchanged()
    {
        Assert.Equal("Ranked", PresetNameHelper.Normalize("Ranked"));
    }
}
