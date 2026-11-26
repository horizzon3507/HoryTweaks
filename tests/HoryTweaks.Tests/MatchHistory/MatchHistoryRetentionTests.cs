using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class MatchHistoryRetentionTests
{
    private static readonly DateTime Start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<string> Names(int count)
    {
        return [.. Enumerable.Range(0, count).Select(i => MatchHistoryRetention.FileNameFor(Start.AddMinutes(i)))];
    }

    [Fact]
    public void SelectForDeletion_keeps_the_newest_and_returns_the_oldest()
    {
        var names = Names(30);
        var shuffled = names.OrderBy(_ => Guid.NewGuid()).ToList();

        var deleted = MatchHistoryRetention.SelectForDeletion(shuffled);

        Assert.Equal(names.Take(5).OrderBy(n => n, StringComparer.Ordinal), deleted.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Foreign_files_are_never_selected_or_counted()
    {
        var names = Names(MatchHistoryRetention.MaxMatches);
        names.Add("notes.txt");
        names.Add("match-bad.htmatch");
        names.Add(MatchHistoryRetention.FileNameFor(Start) + ".tmp");

        Assert.Empty(MatchHistoryRetention.SelectForDeletion(names));
        Assert.Equal(MatchHistoryRetention.MaxMatches, MatchHistoryRetention.NewestFirst(names).Count);
    }

    [Fact]
    public void NewestFirst_orders_by_start_time()
    {
        var names = Names(3);

        Assert.Equal([names[2], names[1], names[0]], MatchHistoryRetention.NewestFirst(names));
    }

    [Fact]
    public void FileNameFor_produces_a_match_file_name()
    {
        Assert.True(MatchHistoryRetention.IsMatchFile(MatchHistoryRetention.FileNameFor(new DateTime(2025, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc))));
    }

    [Fact]
    public void FileNameFor_formats_the_utc_timestamp()
    {
        Assert.Equal(
            "match-20251231T235959007Z.htmatch",
            MatchHistoryRetention.FileNameFor(new DateTime(2025, 12, 31, 23, 59, 59, 7, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("match-20251231T235959007Z.htmatch", true)]
    [InlineData("match-2025123T235959007Z.htmatch", false)]
    [InlineData("match-20251231T23595900Z.htmatch", false)]
    [InlineData("match-20251231T2359590070Z.htmatch", false)]
    [InlineData("match-20251231t235959007Z.htmatch", false)]
    [InlineData("match-20251231T235959007z.htmatch", false)]
    [InlineData("match-20251231T235959007Z.HTMATCH", false)]
    [InlineData("match-20251231T235959007Z.htmatch.bak", false)]
    [InlineData("match-20251231T235959007Z.htmatch ", false)]
    [InlineData("xmatch-20251231T235959007Z.htmatch", false)]
    public void IsMatchFile_matches_only_the_exact_pattern(string name, bool expected)
    {
        Assert.Equal(expected, MatchHistoryRetention.IsMatchFile(name));
    }

    [Fact]
    public void SelectForDeletion_returns_the_oldest_matches_newest_first()
    {
        var names = Names(MatchHistoryRetention.MaxMatches + 5);

        var deleted = MatchHistoryRetention.SelectForDeletion(names);

        Assert.Equal(names.Take(5).Reverse(), deleted);
    }

    [Fact]
    public void SelectForDeletion_honours_a_custom_keep()
    {
        var names = Names(5);

        Assert.Equal([names[2], names[1], names[0]], MatchHistoryRetention.SelectForDeletion(names, keep: 2));
    }

    [Fact]
    public void SelectForDeletion_returns_empty_when_the_limit_is_not_exceeded()
    {
        Assert.Empty(MatchHistoryRetention.SelectForDeletion(Names(MatchHistoryRetention.MaxMatches)));
        Assert.Empty(MatchHistoryRetention.SelectForDeletion([]));
        Assert.Empty(MatchHistoryRetention.NewestFirst([]));
    }

    [Fact]
    public void NewestFirst_orders_matches_within_the_same_second()
    {
        var start = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var a = MatchHistoryRetention.FileNameFor(start.AddMilliseconds(1));
        var b = MatchHistoryRetention.FileNameFor(start.AddMilliseconds(20));
        var c = MatchHistoryRetention.FileNameFor(start.AddMilliseconds(300));

        Assert.Equal([c, b, a], MatchHistoryRetention.NewestFirst([a, c, b]));
    }
}
