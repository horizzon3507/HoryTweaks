using BetterAmongUs.Core.Options;
using Xunit;

namespace HoryTweaks.Tests.Options;

public class NumberOptionRulesTests
{
    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 5)]
    [InlineData(false, true, 10)]
    [InlineData(true, true, 25)]
    public void StepMultiplier_matches_modifier_combinations(bool shift, bool control, int expected)
    {
        Assert.Equal(expected, NumberOptionRules.StepMultiplier(shift, control));
    }

    [Theory]
    [InlineData(5, 0, 10, 5)]
    [InlineData(-3, 0, 10, 0)]
    [InlineData(30, 0, 10, 10)]
    [InlineData(7, 7, 7, 7)]
    public void Clamp_bounds_values(int value, int min, int max, int expected)
    {
        Assert.Equal(expected, NumberOptionRules.Clamp(value, min, max));
    }

    [Fact]
    public void Clamp_throws_when_min_exceeds_max()
    {
        Assert.Throws<ArgumentException>(() => NumberOptionRules.Clamp(5, 10, 0));
    }

    [Theory]
    [InlineData(1.26f, 0f, 10f, 1.3f)]
    [InlineData(1.24f, 0f, 10f, 1.2f)]
    [InlineData(-0.4f, 0f, 10f, 0f)]
    [InlineData(11.7f, 0f, 10f, 10f)]
    [InlineData(3f, 0f, 10f, 3f)]
    public void ClampAndRound_clamps_then_rounds_to_one_decimal(float value, float min, float max, float expected)
    {
        Assert.Equal(expected, NumberOptionRules.ClampAndRound(value, min, max));
    }
}
