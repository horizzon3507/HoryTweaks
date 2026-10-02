using BetterAmongUs.Utilities;
using Xunit;

namespace BetterAmongUs.Tests;

public class SettingsSearchFilterTests
{
    private static SettingsSearchFilter.Row Option(string title, string? description = null, bool normallyVisible = true) =>
        new(IsGroupLabel: false, NormallyVisible: normallyVisible, Title: title, Description: description);

    private static SettingsSearchFilter.Row Label(bool normallyVisible = true) =>
        new(IsGroupLabel: true, NormallyVisible: normallyVisible, Title: null, Description: null);

    [Fact]
    public void Matches_EmptyQueries_MatchEverything()
    {
        Assert.True(SettingsSearchFilter.Matches(null, "Anything", null));
        Assert.True(SettingsSearchFilter.Matches("", "Anything", null));
        Assert.True(SettingsSearchFilter.Matches("   ", "Anything", null));
        Assert.True(SettingsSearchFilter.Matches(null, null, null));
    }

    [Fact]
    public void Matches_TitleAndDescription_CaseInsensitive()
    {
        Assert.True(SettingsSearchFilter.Matches("kick", "Kick Cooldown", null));
        Assert.True(SettingsSearchFilter.Matches("COOLDOWN", "Kick Cooldown", null));
        Assert.True(SettingsSearchFilter.Matches("moderation", "Use ban list", "Moderation actions taken by the host"));
        Assert.False(SettingsSearchFilter.Matches("rejoin", "Kick Cooldown", "Moderation actions"));
        Assert.False(SettingsSearchFilter.Matches("rejoin", null, null));
    }

    [Fact]
    public void Matches_QueryIsTrimmed()
    {
        Assert.True(SettingsSearchFilter.Matches("  kick  ", "Kick Cooldown", null));
    }

    [Fact]
    public void ComputeVisible_EmptyQuery_ShowsEverything()
    {
        var rows = new[] { Label(), Option("One"), Label(), Option("Two") };
        Assert.Equal(new[] { true, true, true, true }, SettingsSearchFilter.ComputeVisible(rows, null));
        Assert.Equal(new[] { true, true, true, true }, SettingsSearchFilter.ComputeVisible(rows, "  "));
    }

    [Fact]
    public void ComputeVisible_HeaderHidesWhenGroupHasNoMatch()
    {
        var rows = new[]
        {
            Label(),
            Option("Kick Cooldown"),
            Label(),
            Option("RPC limit"),
        };

        Assert.Equal(new[] { false, false, true, true }, SettingsSearchFilter.ComputeVisible(rows, "rpc"));
    }

    [Fact]
    public void ComputeVisible_HeaderShowsWhenAnyOptionMatches()
    {
        var rows = new[]
        {
            Label(),
            Option("Kick Cooldown"),
            Option("Invalid Friend Code"),
        };

        Assert.Equal(new[] { true, true, false }, SettingsSearchFilter.ComputeVisible(rows, "kick"));
    }

    [Fact]
    public void ComputeVisible_DescriptionMatchKeepsRowAndHeader()
    {
        var rows = new[]
        {
            Label(),
            Option("Use ban list", "Moderation actions taken by the host"),
        };

        Assert.Equal(new[] { true, true }, SettingsSearchFilter.ComputeVisible(rows, "moderation"));
    }

    [Fact]
    public void ComputeVisible_HiddenOptionDoesNotKeepHeaderAlive()
    {
        var rows = new[]
        {
            Label(),
            Option("Kick Cooldown", normallyVisible: false),
            Option("RPC limit"),
        };

        Assert.Equal(new[] { false, false, false }, SettingsSearchFilter.ComputeVisible(rows, "kick"));
    }

    [Fact]
    public void ComputeVisible_HiddenHeaderStaysHidden()
    {
        var rows = new[]
        {
            Label(normallyVisible: false),
            Option("Kick Cooldown"),
        };

        Assert.Equal(new[] { false, true }, SettingsSearchFilter.ComputeVisible(rows, "kick"));
    }

    [Fact]
    public void ComputeVisible_TrailingLabelHides()
    {
        var rows = new[]
        {
            Option("RPC limit"),
            Label(),
        };

        Assert.Equal(new[] { true, false }, SettingsSearchFilter.ComputeVisible(rows, "rpc"));
    }

    [Fact]
    public void ComputeVisible_AdjacentLabelsTrackOnlyTheirGroup()
    {
        var rows = new[]
        {
            Label(),
            Option("Kick Cooldown"),
            Label(),
            Label(),
            Option("RPC limit"),
        };

        Assert.Equal(new[] { false, false, false, true, true }, SettingsSearchFilter.ComputeVisible(rows, "rpc"));
    }
}
