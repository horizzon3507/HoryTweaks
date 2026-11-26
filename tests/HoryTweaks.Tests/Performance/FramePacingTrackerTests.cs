using HoryTweaks.Core.Performance;
using Xunit;

namespace HoryTweaks.Tests.Performance;

public class FramePacingTrackerTests
{
    [Fact]
    public void Smooth_60_fps_frames_report_no_warnings()
    {
        var tracker = new FramePacingTracker();
        double t = 0.0;

        for (int i = 0; i < 60; i++)
        {
            t += 1.0 / 60.0;
            Assert.Equal(FrameWarningKind.None, tracker.RecordFrame(1f / 60f, t));
        }

        Assert.InRange(tracker.SmoothedFps, 58f, 62f);
        Assert.Equal(0, tracker.HitchCount);
        Assert.Equal(FrameWarningKind.None, tracker.ActiveWarning);
    }

    [Fact]
    public void Severe_frame_hitch_increments_counter_and_emits_cooldown_guarded_warning()
    {
        var tracker = new FramePacingTracker();
        tracker.RecordFrame(0.016f, 1.0);

        Assert.Equal(FrameWarningKind.SevereHitch, tracker.RecordFrame(0.145f, 1.2));
        Assert.Equal(1, tracker.HitchCount);
        Assert.Equal(145, tracker.LastHitchMs);
        Assert.Equal(FrameWarningKind.SevereHitch, tracker.ActiveWarning);

        // Second hitch within cooldown increments count but does not spam warning return.
        Assert.Equal(FrameWarningKind.None, tracker.RecordFrame(0.120f, 2.0));
        Assert.Equal(2, tracker.HitchCount);
    }

    [Fact]
    public void Sustained_low_fps_emits_warning_after_sustain_window()
    {
        var tracker = new FramePacingTracker();
        FrameWarningKind emitted = FrameWarningKind.None;

        // 20 FPS = 50ms per frame for 4 seconds (below hitch threshold, below 30 FPS threshold).
        for (int i = 1; i <= 80; i++)
        {
            var warning = tracker.RecordFrame(0.050f, i * 0.05);
            if (warning != FrameWarningKind.None)
            {
                emitted = warning;
            }
        }

        Assert.Equal(FrameWarningKind.SustainedLowFps, emitted);
        Assert.Equal(FrameWarningKind.SustainedLowFps, tracker.ActiveWarning);
        Assert.True(tracker.SmoothedFps < FramePacingTracker.LowFpsThreshold);
    }
}
