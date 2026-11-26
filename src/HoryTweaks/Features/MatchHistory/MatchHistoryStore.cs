using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Features.MatchHistory.Video;
using HoryTweaks.Infrastructure.Persistence;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// A saved match together with its file name.
/// </summary>
internal sealed record MatchHistoryEntry(string FileName, MatchRecord Record);

/// <summary>
/// Disk storage for recorded matches. Reads are gated by <see cref="MatchHistoryRules.CanView"/>
/// so recorded data is never readable while a match is in progress.
/// </summary>
internal static class MatchHistoryStore
{
    private const string LogTag = "MatchHistory";
    private const string TempExtension = ".tmp";

    private static string Folder => BetterDataManager.Folders.matchHistoryFolderPath;

    /// <summary>
    /// Saves a finished match atomically and prunes files beyond the retention limit.
    /// </summary>
    internal static bool Save(MatchRecord record)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            string finalPath = Path.Combine(Folder, MatchHistoryRetention.FileNameFor(record.StartedUtc));
            string tempPath = finalPath + TempExtension;
            File.WriteAllBytes(tempPath, MatchRecordCodec.Encode(record));
            File.Move(tempPath, finalPath, true);

            foreach (string fileName in MatchHistoryRetention.SelectForDeletion(Directory.GetFiles(Folder).Select(Path.GetFileName).OfType<string>()))
            {
                File.Delete(Path.Combine(Folder, fileName));
            }

            BAUPlugin.Logger.Log($"Saved match history ({record.Events.Count} events, {record.Frames.Count} frames)", LogTag);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            return false;
        }
    }

    /// <summary>
    /// Lists saved matches newest first, without position data. Empty while a match is running.
    /// </summary>
    internal static IReadOnlyList<MatchHistoryEntry> List()
    {
        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current) || !Directory.Exists(Folder))
            return [];

        var entries = new List<MatchHistoryEntry>();
        try
        {
            var fileNames = MatchHistoryRetention.NewestFirst(Directory.GetFiles(Folder).Select(Path.GetFileName).OfType<string>());
            foreach (string fileName in fileNames)
            {
                var record = ReadRecord(fileName, includeFrames: false);
                if (record != null)
                {
                    entries.Add(new MatchHistoryEntry(fileName, record));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }

        return entries;
    }

    /// <summary>
    /// Loads a full match including positions. <c>null</c> while a match is running or when unreadable.
    /// </summary>
    internal static MatchRecord? Load(string fileName)
    {
        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current) || !MatchHistoryRetention.IsMatchFile(fileName))
            return null;

        return ReadRecord(fileName, includeFrames: true);
    }

    /// <summary>
    /// Returns the paired POV MP4 path for <paramref name="matchFileName"/> when viewing is allowed and the video exists.
    /// </summary>
    internal static bool TryGetVideoPath(string matchFileName, out string videoPath)
    {
        videoPath = string.Empty;
        if (!MatchHistoryRules.CanView(MatchHistoryGameState.Current))
            return false;

        string? videoFile = MatchVideoRetention.VideoFileNameForMatchFile(matchFileName);
        if (videoFile == null)
            return false;

        string fullPath = Path.Combine(Folder, videoFile);
        if (!File.Exists(fullPath))
            return false;

        videoPath = fullPath;
        return true;
    }

    /// <summary>
    /// Enforces count and total disk-size limits on saved MP4 replays and cleans up stale temporary files.
    /// </summary>
    internal static void PruneVideos()
    {
        if (!Directory.Exists(Folder))
            return;

        try
        {
            var videoEntries = new List<(string FileName, long SizeBytes)>();
            foreach (string path in Directory.GetFiles(Folder))
            {
                string fileName = Path.GetFileName(path);
                if (MatchVideoRetention.IsVideoFile(fileName))
                {
                    videoEntries.Add((fileName, new FileInfo(path).Length));
                }
                else if (!MatchVideoRecorder.IsCapturing && fileName.EndsWith(MatchVideoRecorder.TempSuffix, StringComparison.Ordinal))
                {
                    try { File.Delete(path); } catch { }
                }
            }

            foreach (string toDelete in MatchVideoRetention.SelectForDeletion(videoEntries))
            {
                try { File.Delete(Path.Combine(Folder, toDelete)); } catch { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }
    }

    /// <summary>
    /// Deletes every saved match, POV video replay and leftover temporary file.
    /// </summary>
    internal static void DeleteAll()
    {
        if (!Directory.Exists(Folder))
            return;

        try
        {
            foreach (string path in Directory.GetFiles(Folder))
            {
                string fileName = Path.GetFileName(path);
                if (MatchHistoryRetention.IsMatchFile(fileName)
                    || MatchVideoRetention.IsVideoFile(fileName)
                    || fileName.EndsWith(TempExtension, StringComparison.Ordinal)
                    || fileName.EndsWith(MatchVideoRecorder.TempSuffix, StringComparison.Ordinal))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
        }
    }

    private static MatchRecord? ReadRecord(string fileName, bool includeFrames)
    {
        try
        {
            return MatchRecordCodec.Decode(File.ReadAllBytes(Path.Combine(Folder, fileName)), includeFrames);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            return null;
        }
    }
}
