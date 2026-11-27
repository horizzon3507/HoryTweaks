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

    [Fact]
    public void Scene_transition_grace_period_suppresses_load_frame_hitches()
    {
        var tracker = new FramePacingTracker();
        tracker.RecordFrame(0.016f, 1.0);

        tracker.NotifySceneTransition(1.0);

        // A 337ms scene-load stall inside the 2.5s grace window must not count as a hitch.
        Assert.Equal(FrameWarningKind.None, tracker.RecordFrame(0.337f, 1.1));
        Assert.Equal(0, tracker.HitchCount);
        Assert.Equal(0, tracker.LastHitchMs);
        Assert.Equal(FrameWarningKind.None, tracker.ActiveWarning);
    }

    [Fact]
    public void Grace_period_does_not_feed_load_stall_deltas_into_smoothed_fps()
    {
        var tracker = new FramePacingTracker();

        // Warm up EMA at 60 FPS before the transition.
        for (int i = 0; i < 30; i++)
        {
            tracker.RecordFrame(1f / 60f, i * (1.0 / 60.0));
        }

        tracker.NotifySceneTransition(0.5);

        // Load stalls during grace are excluded from the EMA.
        tracker.RecordFrame(0.5f, 0.6);
        tracker.RecordFrame(0.4f, 1.0);

        Assert.InRange(tracker.SmoothedFps, 55f, 65f);
    }

    [Fact]
    public void Hitch_detection_resumes_after_grace_window_expires()
    {
        var tracker = new FramePacingTracker();
        tracker.RecordFrame(0.016f, 1.0);

        tracker.NotifySceneTransition(1.0);

        // Still inside the grace window (1.0 + 2.5 = 3.5).
        Assert.Equal(FrameWarningKind.None, tracker.RecordFrame(0.2f, 3.4));
        Assert.Equal(0, tracker.HitchCount);

        // After the window, hitches count and warn again.
        Assert.Equal(FrameWarningKind.SevereHitch, tracker.RecordFrame(0.2f, 3.6));
        Assert.Equal(1, tracker.HitchCount);
        Assert.Equal(200, tracker.LastHitchMs);
    }

    [Fact]
    public void NotifySceneTransition_extends_existing_grace_window_and_reset_clears_it()
    {
        var tracker = new FramePacingTracker();
        tracker.RecordFrame(0.016f, 1.0);

        tracker.NotifySceneTransition(1.0);
        tracker.NotifySceneTransition(2.0);

        // Window now extends to 2.0 + 2.5 = 4.5.
        Assert.Equal(FrameWarningKind.None, tracker.RecordFrame(0.2f, 4.4));
        Assert.Equal(0, tracker.HitchCount);
        Assert.Equal(FrameWarningKind.SevereHitch, tracker.RecordFrame(0.2f, 4.6));
        Assert.Equal(1, tracker.HitchCount);

        tracker.Reset();
        Assert.Equal(FrameWarningKind.SevereHitch, tracker.RecordFrame(0.2f, 5.0));
        Assert.Equal(1, tracker.HitchCount);
    }
}
