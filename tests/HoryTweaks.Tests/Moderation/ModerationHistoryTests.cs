using BetterAmongUs.Core.Moderation;
using Xunit;

namespace HoryTweaks.Tests.Moderation;

[Collection("ModerationHistory")]
public class ModerationHistoryTests : IDisposable
{
    public ModerationHistoryTests() => ModerationHistory.Clear();

    public void Dispose() => ModerationHistory.Clear();

    [Fact]
    public void RecordStripsRichTextAndKeepsIdentity()
    {
        ModerationHistory.Record(ModerationActionKind.Ban, "<color=#ff0000>Red</color>", "ABCD#1234", "line one\nline two");

        var entry = Assert.Single(ModerationHistory.Entries);
        Assert.Equal(ModerationActionKind.Ban, entry.Action);
        Assert.Equal("Red", entry.PlayerName);
        Assert.Equal("ABCD#1234", entry.Identity);
        Assert.Equal("line one line two", entry.Reason);
    }

    [Fact]
    public void LatestReturnsNewestFirstAndRespectsCount()
    {
        for (int i = 0; i < 5; i++)
        {
            ModerationHistory.Record(ModerationActionKind.Kick, $"Player{i}", string.Empty, string.Empty);
        }

        var latest = ModerationHistory.Latest(2).Select(e => e.PlayerName).ToArray();
        Assert.Equal(new[] { "Player4", "Player3" }, latest);
    }

    [Fact]
    public void HistoryIsCappedAtMaxEntries()
    {
        for (int i = 0; i < ModerationHistory.MaxEntries + 10; i++)
        {
            ModerationHistory.Record(ModerationActionKind.Kick, $"Player{i}", string.Empty, string.Empty);
        }

        Assert.Equal(ModerationHistory.MaxEntries, ModerationHistory.Entries.Count);
        Assert.Equal("Player10", ModerationHistory.Entries[0].PlayerName);
    }

    [Fact]
    public void PendingReasonIsConsumedOnce()
    {
        ModerationHistory.SetPendingReason("reason");

        Assert.Equal("reason", ModerationHistory.TakePendingReason());
        Assert.Equal(string.Empty, ModerationHistory.TakePendingReason());
    }
}
