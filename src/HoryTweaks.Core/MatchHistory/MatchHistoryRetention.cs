using System.Globalization;
using System.Text.RegularExpressions;

namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// File naming and retention for saved matches. Names sort chronologically, so ordinal order is age order.
/// </summary>
internal static class MatchHistoryRetention
{
    /// <summary>
    /// Number of matches kept on disk.
    /// </summary>
    internal const int MaxMatches = 25;

    private static readonly Regex MatchFilePattern = new(@"^match-\d{8}T\d{9}Z\.htmatch$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Builds the file name for a match that started at <paramref name="startedUtc"/>.
    /// </summary>
    internal static string FileNameFor(DateTime startedUtc)
    {
        return "match-" + startedUtc.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".htmatch";
    }

    /// <summary>
    /// Determines whether a file name was produced by <see cref="FileNameFor"/>.
    /// </summary>
    internal static bool IsMatchFile(string fileName)
    {
        return MatchFilePattern.IsMatch(fileName);
    }

    /// <summary>
    /// Returns the match files among <paramref name="fileNames"/>, newest first.
    /// </summary>
    internal static IReadOnlyList<string> NewestFirst(IEnumerable<string> fileNames)
    {
        return [.. fileNames.Where(IsMatchFile).OrderByDescending(name => name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Returns the match files that exceed the retention limit, oldest last.
    /// </summary>
    internal static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> fileNames, int keep = MaxMatches)
    {
        return [.. NewestFirst(fileNames).Skip(keep)];
    }
}
