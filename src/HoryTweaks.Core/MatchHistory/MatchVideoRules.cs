namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Geometry, rate and recording gates for real-POV MP4 match video replays.
/// </summary>
internal static class MatchVideoRules
{
    /// <summary>
    /// Target capture frame rate. 15 fps keeps Among Us legible while costing 2.35x fewer pixels/sec
    /// than the original 864x480@20 budget for both GPU readback and CPU encoding.
    /// </summary>
    internal const int TargetFps = 15;

    /// <summary>
    /// Lowest adaptive capture frame rate the scheduler may step down to before giving up.
    /// </summary>
    internal const int MinAdaptiveFps = 5;

    /// <summary>
    /// Maximum encoded video dimensions (multiples of 16 for H.264 hardware encoders).
    /// </summary>
    internal const int MaxWidth = 640;
    internal const int MaxHeight = 360;
    internal const int MinWidth = 160;
    internal const int MinHeight = 112;

    /// <summary>
    /// Target average H.264 bitrate (~5.2 MB/minute maximum, typically ~2-4 MB/minute for 2D art).
    /// </summary>
    internal const int TargetBitrateBps = 550_000;

    /// <summary>
    /// Bounded queue depth for raw captured frames waiting for the background encoder thread.
    /// </summary>
    internal const int FrameQueueCapacity = 3;

    /// <summary>
    /// Maximum video duration in milliseconds, aligned with 2D match history.
    /// </summary>
    internal const int MaxDurationMs = MatchHistoryRules.MaxDurationMs;

    /// <summary>
    /// Minimum captured duration in milliseconds for a partial video to be worth finalizing.
    /// </summary>
    internal const int MinSaveableDurationMs = 5_000;

    /// <summary>
    /// Minimum encoded frame count for a partial video to be worth finalizing.
    /// </summary>
    internal const int MinSaveableFrames = 15;

    /// <summary>
    /// Maximum milliseconds of main-thread time a single capture step may consume before it counts as slow.
    /// </summary>
    internal const double MaxMainThreadCaptureMs = 12.0;

    /// <summary>
    /// Consecutive slow captures at minimum adaptive FPS after which capture stops to protect gameplay.
    /// </summary>
    internal const int AutoPauseConsecutiveSlowCaptures = 6;

    /// <summary>
    /// Smoothed game FPS below which capture is considered harmful.
    /// </summary>
    internal const float AutoPauseLowFpsThreshold = 20f;

    /// <summary>
    /// Seconds of sustained low FPS before capture steps down or stops.
    /// </summary>
    internal const double AutoPauseLowFpsDurationSeconds = 4.0;

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
            return (640, 360);

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
