namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Geometry, rate and recording gates for real-POV MP4 match video replays.
/// </summary>
internal static class MatchVideoRules
{
    /// <summary>
    /// Target capture frame rate. 20 fps keeps Among Us smooth while staying light on CPU and disk.
    /// </summary>
    internal const int TargetFps = 20;

    /// <summary>
    /// Maximum encoded video dimensions (multiples of 16 for H.264 hardware encoders).
    /// </summary>
    internal const int MaxWidth = 864;
    internal const int MaxHeight = 480;
    internal const int MinWidth = 160;
    internal const int MinHeight = 112;

    /// <summary>
    /// Target average H.264 bitrate (~5.2 MB/minute maximum, typically ~2-4 MB/minute for 2D art).
    /// </summary>
    internal const int TargetBitrateBps = 700_000;

    /// <summary>
    /// Bounded queue depth for raw captured frames waiting for the background encoder thread.
    /// </summary>
    internal const int FrameQueueCapacity = 4;

    /// <summary>
    /// Maximum video duration in milliseconds, aligned with 2D match history.
    /// </summary>
    internal const int MaxDurationMs = MatchHistoryRules.MaxDurationMs;

    /// <summary>
    /// Determines whether video capture may begin for the current match.
    /// </summary>
    internal static bool ShouldBeginRecording(
        bool matchHistoryEnabled,
        bool videoReplayEnabled,
        bool armed,
        bool freePlay,
        ClientGameState? state,
        bool shipPresent,
        bool introPlaying)
    {
        return videoReplayEnabled
            && MatchHistoryRules.ShouldBeginRecording(matchHistoryEnabled, armed, freePlay, state, shipPresent, introPlaying);
    }

    /// <summary>
    /// Computes aspect-preserving output dimensions aligned to 16-pixel macroblocks within the configured cap.
    /// </summary>
    internal static (int Width, int Height) ComputeScaledDimensions(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0)
            return (848, 480);

        double scale = Math.Min(1.0, Math.Min((double)MaxWidth / screenWidth, (double)MaxHeight / screenHeight));
        int scaledW = (int)Math.Round(screenWidth * scale);
        int scaledH = (int)Math.Round(screenHeight * scale);

        int width = Align16(scaledW, MinWidth, MaxWidth);
        int height = Align16(scaledH, MinHeight, MaxHeight);
        return (width, height);
    }

    private static int Align16(int value, int min, int max)
    {
        int rounded = ((value + 8) / 16) * 16;
        return Math.Clamp(rounded, min, max);
    }
}
