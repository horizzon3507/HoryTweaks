namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Pure pacing helper for constant-framerate rawvideo encoder pipes (ffmpeg stdin). Raw pipes carry
/// no timestamps, so when capture FPS steps down (15 -&gt; 10 -&gt; 5) frames would otherwise play back
/// too fast. The pacer computes how many copies of the previous frame must be written before each new
/// frame so the constant-rate output keeps 1x real-time duration, capped to avoid pipe stalls.
/// </summary>
internal sealed class FfmpegFramePacer
{
    internal const int DefaultMaxDuplicatesPerGap = 30;

    private readonly long _frameDuration100ns;
    private readonly int _maxDuplicates;
    private long _lastTimestamp100ns;
    private bool _hasLastFrame;

    internal FfmpegFramePacer(long frameDuration100ns, int maxDuplicatesPerGap = DefaultMaxDuplicatesPerGap)
    {
        if (frameDuration100ns <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameDuration100ns));

        _frameDuration100ns = frameDuration100ns;
        _maxDuplicates = Math.Max(maxDuplicatesPerGap, 0);
    }

    /// <summary>
    /// Returns how many copies of the previous frame to emit before writing the frame at
    /// <paramref name="timestamp100ns"/>. Always 0 for the first frame.
    /// </summary>
    internal int ComputeDuplicatesBeforeNextFrame(long timestamp100ns)
    {
        if (!_hasLastFrame)
        {
            _hasLastFrame = true;
            _lastTimestamp100ns = timestamp100ns;
            return 0;
        }

        long gap = timestamp100ns - _lastTimestamp100ns;
        _lastTimestamp100ns = timestamp100ns;

        if (gap <= 0)
            return 0;

        // Slots between the previous and the new frame, rounded to the nearest output frame interval.
        long slots = (gap + (_frameDuration100ns / 2)) / _frameDuration100ns;
        long duplicates = slots - 1;
        if (duplicates <= 0)
            return 0;

        return (int)Math.Min(duplicates, _maxDuplicates);
    }
}
