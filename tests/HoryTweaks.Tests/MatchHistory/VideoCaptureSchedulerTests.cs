using HoryTweaks.Core.MatchHistory;
using Xunit;

namespace HoryTweaks.Tests.MatchHistory;

public class VideoCaptureSchedulerTests
{
    [Fact]
    public void ShouldStartCapture_waits_for_encoder_ready()
    {
        var scheduler = new VideoCaptureScheduler();

        Assert.False(scheduler.ShouldStartCapture(10.0, encoderReady: false, inFlightCount: 0, maxInFlight: 2));
        Assert.True(scheduler.ShouldStartCapture(10.0, encoderReady: true, inFlightCount: 0, maxInFlight: 2));
    }

    [Fact]
    public void ShouldStartCapture_respects_inflight_concurrency_cap()
    {
        var scheduler = new VideoCaptureScheduler();

        Assert.False(scheduler.ShouldStartCapture(10.0, encoderReady: true, inFlightCount: 2, maxInFlight: 2));
        Assert.True(scheduler.ShouldStartCapture(10.0, encoderReady: true, inFlightCount: 1, maxInFlight: 2));
    }

    [Fact]
    public void ShouldStartCapture_waits_for_next_capture_time()
    {
        var scheduler = new VideoCaptureScheduler();
        scheduler.RecordCaptureScheduled(10.0);

        // Interval at 15 fps is ~66.7ms.
        Assert.False(scheduler.ShouldStartCapture(10.03, encoderReady: true, inFlightCount: 0, maxInFlight: 2));
        Assert.True(scheduler.ShouldStartCapture(10.07, encoderReady: true, inFlightCount: 0, maxInFlight: 2));
    }

    [Fact]
    public void RecordCaptureScheduled_never_death_spirals()
    {
        var scheduler = new VideoCaptureScheduler();
        scheduler.RecordCaptureScheduled(10.0);

        // Even after a long stall, the next capture is strictly now + 1/fps, never catch-up bursts.
        double stallEnd = 30.0;
        scheduler.RecordCaptureScheduled(stallEnd);
        double expected = stallEnd + (1.0 / scheduler.CurrentFps);
        Assert.Equal(expected, scheduler.NextCaptureTimeSec, precision: 9);
    }

    [Fact]
    public void Slow_mainthread_captures_step_down_fps_and_stop_at_min_fps()
    {
        var scheduler = new VideoCaptureScheduler();
        Assert.Equal(15, scheduler.CurrentFps);

        // 20ms of main-thread capture work far exceeds the 12ms budget.
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(20.0, 0.016, i * 0.1));
        }

        Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(20.0, 0.016, 0.3));
        Assert.Equal(10, scheduler.CurrentFps);

        Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(20.0, 0.016, 0.4));
        Assert.Equal(5, scheduler.CurrentFps);

        // Continue slow captures at min FPS: streak reaches 6 -> stop.
        VideoCaptureStopReason reason = VideoCaptureStopReason.None;
        for (int i = 5; i <= 7 && reason == VideoCaptureStopReason.None; i++)
        {
            reason = scheduler.RecordMainThreadWork(20.0, 0.016, i * 0.1);
        }

        Assert.Equal(VideoCaptureStopReason.MainThreadBudgetExceeded, reason);
        Assert.True(scheduler.IsStopped);
        Assert.False(scheduler.ShouldStartCapture(100.0, encoderReady: true, inFlightCount: 0, maxInFlight: 2));
    }

    [Fact]
    public void Fast_captures_decay_slow_streak()
    {
        var scheduler = new VideoCaptureScheduler();
        scheduler.RecordMainThreadWork(20.0, 0.016, 0.0);
        scheduler.RecordMainThreadWork(20.0, 0.016, 0.1);
        Assert.Equal(2, scheduler.SlowCaptureStreak);

        scheduler.RecordMainThreadWork(2.0, 0.016, 0.2);
        Assert.Equal(1, scheduler.SlowCaptureStreak);
        Assert.Equal(15, scheduler.CurrentFps);
    }

    [Fact]
    public void Sustained_low_fps_steps_down_then_stops_with_sustained_low_fps()
    {
        var scheduler = new VideoCaptureScheduler();

        // Feed ~15 FPS frame deltas (66ms) for over 4 seconds to trigger first step-down.
        double t = 0;
        VideoCaptureStopReason reason = VideoCaptureStopReason.None;
        for (int i = 0; i < 70 && scheduler.CurrentFps == 15; i++)
        {
            t += 0.066;
            reason = scheduler.RecordMainThreadWork(2.0, 0.066, t);
        }

        Assert.Equal(VideoCaptureStopReason.None, reason);
        Assert.Equal(10, scheduler.CurrentFps);

        // Continue low FPS: step down to 5 after another 4s window.
        for (int i = 0; i < 70 && scheduler.CurrentFps == 10; i++)
        {
            t += 0.066;
            reason = scheduler.RecordMainThreadWork(2.0, 0.066, t);
        }

        Assert.Equal(5, scheduler.CurrentFps);

        // At min FPS with continued low game FPS, capture stops.
        for (int i = 0; i < 70 && reason == VideoCaptureStopReason.None; i++)
        {
            t += 0.066;
            reason = scheduler.RecordMainThreadWork(2.0, 0.066, t);
        }

        Assert.Equal(VideoCaptureStopReason.SustainedLowFps, reason);
    }

    [Fact]
    public void Healthy_frames_recover_fps_upward_after_ten_seconds()
    {
        var scheduler = new VideoCaptureScheduler();

        // Step down once with slow captures.
        for (int i = 0; i < 3; i++)
        {
            scheduler.RecordMainThreadWork(20.0, 0.016, i * 0.1);
        }

        Assert.Equal(10, scheduler.CurrentFps);

        // Now feed fast work and 60fps frames for over 10 seconds.
        double t = 1.0;
        for (int i = 0; i < 700 && scheduler.CurrentFps == 10; i++)
        {
            t += 0.016;
            scheduler.RecordMainThreadWork(2.0, 0.016, t);
        }

        Assert.Equal(15, scheduler.CurrentFps);
    }

    [Fact]
    public void Reset_clears_all_state()
    {
        var scheduler = new VideoCaptureScheduler();
        for (int i = 0; i < 8; i++)
        {
            scheduler.RecordMainThreadWork(20.0, 0.016, i * 0.1);
        }

        Assert.True(scheduler.IsStopped);

        scheduler.Reset();
        Assert.Equal(15, scheduler.CurrentFps);
        Assert.Equal(0, scheduler.SlowCaptureStreak);
        Assert.Equal(VideoCaptureStopReason.None, scheduler.StopReason);
        Assert.False(scheduler.IsStopped);
        Assert.Equal(0, scheduler.NextCaptureTimeSec);
        Assert.True(scheduler.ShouldStartCapture(1.0, encoderReady: true, inFlightCount: 0, maxInFlight: 2));
    }

    [Fact]
    public void Invalid_frame_deltas_are_ignored()
    {
        var scheduler = new VideoCaptureScheduler();

        Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(2.0, double.NaN, 1.0));
        Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(2.0, double.PositiveInfinity, 1.1));
        Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(2.0, -1.0, 1.2));
        Assert.Equal(VideoCaptureStopReason.None, scheduler.RecordMainThreadWork(2.0, 0.0, 1.3));
    }
}
