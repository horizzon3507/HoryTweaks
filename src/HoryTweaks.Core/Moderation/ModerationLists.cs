namespace BetterAmongUs.Core.Moderation;

/// <summary>
/// The plain-text ban lists kept under Better_Data.
/// </summary>
internal enum ModerationListKind
{
    Player,
    Name,
    Chat
}

/// <summary>
/// Format helpers for the ban list files. The files keep their existing line-based format.
/// </summary>
internal static class ModerationLists
{
    /// <summary>
    /// Counts the lines that the ban checks use: blank lines and lines starting with // or # are ignored.
    /// </summary>
    internal static int CountEntries(IEnumerable<string> lines)
    {
        int count = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//") || trimmed.StartsWith('#'))
                continue;

            count++;
        }

        return count;
    }
}
