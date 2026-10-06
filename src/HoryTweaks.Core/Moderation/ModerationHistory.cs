using System.Text.RegularExpressions;

namespace HoryTweaks.Core.Moderation;

/// <summary>
/// One kick or ban issued by the local host during this session.
/// </summary>
internal sealed record ModerationHistoryEntry(
    DateTime Time,
    ModerationActionKind Action,
    string PlayerName,
    string Identity,
    string Reason);

/// <summary>
/// In-memory log of the kicks and bans issued while the game is running. Nothing is written to disk.
/// </summary>
internal static class ModerationHistory
{
    internal const int MaxEntries = 50;

    private static readonly List<ModerationHistoryEntry> entries = [];
    private static readonly Regex richTextTag = new("<[^>]*>", RegexOptions.Compiled);
    private static string pendingReason = string.Empty;

    internal static IReadOnlyList<ModerationHistoryEntry> Entries => entries;

    internal static void Record(ModerationActionKind action, string playerName, string identity, string reason, DateTime? time = null)
    {
        entries.Add(new ModerationHistoryEntry(
            time ?? DateTime.Now,
            action,
            StripRichText(playerName),
            identity ?? string.Empty,
            StripRichText(reason)));

        while (entries.Count > MaxEntries)
        {
            entries.RemoveAt(0);
        }
    }

    /// <summary>
    /// Returns the newest entries first.
    /// </summary>
    internal static IEnumerable<ModerationHistoryEntry> Latest(int count)
    {
        for (int i = entries.Count - 1; i >= 0 && count > 0; i--, count--)
        {
            yield return entries[i];
        }
    }

    internal static void Clear()
    {
        entries.Clear();
        pendingReason = string.Empty;
    }

    /// <summary>
    /// Stores the reason for the kick that is about to be sent so the kick hook can attach it to the history entry.
    /// </summary>
    internal static void SetPendingReason(string reason) => pendingReason = reason ?? string.Empty;

    internal static string TakePendingReason()
    {
        string reason = pendingReason;
        pendingReason = string.Empty;
        return reason;
    }

    internal static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return richTextTag.Replace(text, string.Empty).Replace('\n', ' ').Trim();
    }
}
