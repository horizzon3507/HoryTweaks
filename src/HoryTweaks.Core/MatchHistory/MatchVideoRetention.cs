using System.Globalization;
using System.Text.RegularExpressions;

namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// File naming and disk-bounded retention for POV MP4 match replays paired with <see cref="MatchHistoryRetention"/>.
/// </summary>
internal static class MatchVideoRetention
{
    /// <summary>
    /// Maximum number of MP4 replays kept on disk.
    /// </summary>
    internal const int MaxVideos = 5;

    /// <summary>
    /// Maximum cumulative size of kept MP4 replays (250 MiB).
    /// </summary>
    internal const long MaxTotalBytes = 250L * 1024 * 1024;

    private static readonly Regex VideoFilePattern = new(@"^match-\d{8}T\d{9}Z\.mp4$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Builds the MP4 file name paired with the match started at <paramref name="startedUtc"/>.
    /// </summary>
    internal static string FileNameFor(DateTime startedUtc)
    {
        return "match-" + startedUtc.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".mp4";
    }

    /// <summary>
    /// Maps a validated <c>.htmatch</c> file name to its paired <c>.mp4</c> file name, or <c>null</c> if invalid.
    /// </summary>
    internal static string? VideoFileNameForMatchFile(string matchFileName)
    {
        if (!MatchHistoryRetention.IsMatchFile(matchFileName))
            return null;

        return matchFileName[..^".htmatch".Length] + ".mp4";
    }

    /// <summary>
    /// Determines whether a file name was produced by <see cref="FileNameFor"/>.
    /// </summary>
    internal static bool IsVideoFile(string fileName)
    {
        return VideoFilePattern.IsMatch(fileName);
    }

    /// <summary>
    /// Parses the UTC recording start time embedded in a valid video file name.
    /// </summary>
    internal static bool TryParseStartedUtc(string videoFileName, out DateTime startedUtc)
    {
        startedUtc = default;
        if (!IsVideoFile(videoFileName))
            return false;

        // Strip "match-" prefix and ".mp4" suffix to get the yyyyMMdd'T'HHmmssfff'Z' timestamp.
        string stamp = videoFileName["match-".Length..^".mp4".Length];
        return DateTime.TryParseExact(
            stamp,
            "yyyyMMdd'T'HHmmssfff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out startedUtc);
    }

    /// <summary>
    /// Selects oldest video files to delete so both the count limit and total byte cap are respected.
    /// Always keeps at least the newest single video if at least one valid file is present and <paramref name="maxVideos"/> &gt; 0.
    /// </summary>
    internal static IReadOnlyList<string> SelectForDeletion(
        IEnumerable<(string FileName, long SizeBytes)> files,
        int maxVideos = MaxVideos,
        long maxTotalBytes = MaxTotalBytes)
    {
        var ordered = files
            .Where(f => IsVideoFile(f.FileName))
            .OrderByDescending(f => f.FileName, StringComparer.Ordinal)
            .ToList();

        if (maxVideos <= 0)
            return [.. ordered.Select(f => f.FileName)];

        var delete = new List<string>();
        int kept = 0;
        long totalBytes = 0;

        foreach (var (fileName, sizeBytes) in ordered)
        {
            long safeSize = Math.Max(sizeBytes, 0L);
            bool withinCount = kept < maxVideos;
            bool withinBytes = kept == 0 || (maxTotalBytes > 0 && totalBytes + safeSize <= maxTotalBytes);

            if (withinCount && withinBytes)
            {
                kept++;
                totalBytes += safeSize;
            }
            else
            {
                delete.Add(fileName);
            }
        }

        return delete;
    }
}
