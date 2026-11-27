using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class FfmpegFramePacerTests
{
    private const long FrameDuration15fps = 10_000_000L / 15;

    [Fact]
    public void Perfect_15fps_cadence_never_duplicates()
    {
        var pacer = new FfmpegFramePacer(FrameDuration15fps);
        long totalFrames = 0;

        for (int i = 0; i < 90; i++)
        {
            int duplicates = pacer.ComputeDuplicatesBeforeNextFrame(i * FrameDuration15fps);
            Assert.Equal(0, duplicates);
            totalFrames += 1 + duplicates;
        }

        Assert.Equal(90, totalFrames);
    }

    [Fact]
    public void Step_down_to_5fps_emits_two_duplicates_per_frame_preserving_realtime()
    {
        var pacer = new FfmpegFramePacer(FrameDuration15fps);
        long captureDuration = 10_000_000L / 5; // 5fps capture spacing
        long totalOutputFrames = 0;
        int capturedFrames = 0;

        // Simulate 6 seconds of 5fps capture (30 frames) piped into a constant 15fps encoder.
        for (int i = 0; i < 30; i++)
        {
            int duplicates = pacer.ComputeDuplicatesBeforeNextFrame(i * captureDuration);
            totalOutputFrames += 1 + duplicates;
            capturedFrames++;
        }

        Assert.Equal(30, capturedFrames);
        // 2 duplicates per frame in steady state: 30 frames + 29 gaps x 2 duplicates = 88 output
        // frames covering 5.87s of the 5.8s capture span (the final frame's trailing pad is only
        // emitted once a following frame arrives, exactly like live capture).
        Assert.Equal(88, totalOutputFrames);
        double playbackSeconds = totalOutputFrames / 15.0;
        Assert.InRange(playbackSeconds, 5.7, 6.1);

        // Steady-state duplicates per frame after the first is exactly 2.
        var steady = new FfmpegFramePacer(FrameDuration15fps);
        steady.ComputeDuplicatesBeforeNextFrame(0);
        Assert.Equal(2, steady.ComputeDuplicatesBeforeNextFrame(captureDuration));
        Assert.Equal(2, steady.ComputeDuplicatesBeforeNextFrame(2 * captureDuration));
    }

    [Fact]
    public void Large_pause_is_capped_at_max_duplicates()
    {
        var pacer = new FfmpegFramePacer(FrameDuration15fps);
        pacer.ComputeDuplicatesBeforeNextFrame(0);

        // A 60-second pause would need 900 duplicates uncapped.
        int duplicates = pacer.ComputeDuplicatesBeforeNextFrame(600_000_000L);
        Assert.Equal(FfmpegFramePacer.DefaultMaxDuplicatesPerGap, duplicates);
        Assert.True(duplicates <= 30);
    }

    [Fact]
    public void First_frame_and_backward_timestamps_never_duplicate()
    {
        var pacer = new FfmpegFramePacer(FrameDuration15fps);

        Assert.Equal(0, pacer.ComputeDuplicatesBeforeNextFrame(5_000_000));
        // Non-monotonic timestamp (should never happen, but must not explode).
        Assert.Equal(0, pacer.ComputeDuplicatesBeforeNextFrame(1_000_000));
    }

    [Fact]
    public void Subframe_jitter_rounds_to_nearest_slot()
    {
        var pacer = new FfmpegFramePacer(FrameDuration15fps);
        pacer.ComputeDuplicatesBeforeNextFrame(0);

        // 1.4 frame intervals of jitter -> still one slot, no duplicates.
        Assert.Equal(0, pacer.ComputeDuplicatesBeforeNextFrame((long)(FrameDuration15fps * 1.4)));
        // 1.6 intervals -> rounds to two slots, one duplicate.
        Assert.Equal(1, pacer.ComputeDuplicatesBeforeNextFrame((long)(FrameDuration15fps * 1.4 + FrameDuration15fps * 1.6)));
    }

    [Fact]
    public void Constructor_rejects_nonpositive_frame_duration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FfmpegFramePacer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FfmpegFramePacer(-1));
    }
}
