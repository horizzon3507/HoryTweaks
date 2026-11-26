using HoryTweaks.Core.Networking;
using Xunit;

namespace HoryTweaks.Tests.Networking;

public class ConnectionHealthTrackerTests
{
    [Fact]
    public void Stable_low_ping_stays_good_with_no_warnings()
    {
        var tracker = new ConnectionHealthTracker();

        for (int i = 0; i < 8; i++)
        {
            var warning = tracker.RecordSample(42 + (i % 3), totalResends: 0, nowSeconds: i * 0.5);
            Assert.Equal(ConnectionWarningKind.None, warning);
        }

        Assert.Equal(ConnectionQuality.Good, tracker.Quality);
        Assert.Equal(ConnectionWarningKind.None, tracker.ActiveWarning);
        Assert.True(tracker.JitterMs < 5f);
        Assert.Equal(0, tracker.WarningCount);
    }

    [Fact]
    public void Sustained_high_ping_emits_warning_once_and_respects_cooldown()
    {
        var tracker = new ConnectionHealthTracker();

        Assert.Equal(ConnectionWarningKind.None, tracker.RecordSample(210, 0, 1.0));
        Assert.Equal(ConnectionWarningKind.None, tracker.RecordSample(215, 0, 1.5));

        // Third consecutive high-ping sample triggers warning.
        Assert.Equal(ConnectionWarningKind.HighPing, tracker.RecordSample(220, 0, 2.0));
        Assert.Equal(ConnectionQuality.HighPing, tracker.Quality);

        // Next sample inside cooldown does not re-emit.
        Assert.Equal(ConnectionWarningKind.None, tracker.RecordSample(225, 0, 2.5));

        // After cooldown expires, warning can fire again.
        Assert.Equal(ConnectionWarningKind.HighPing, tracker.RecordSample(230, 0, 2.0 + ConnectionHealthTracker.WarningCooldownSeconds));
        Assert.Equal(2, tracker.WarningCount);
    }

    [Fact]
    public void Resend_burst_flags_packet_loss_immediately()
    {
        var tracker = new ConnectionHealthTracker();
        tracker.RecordSample(45, 2, 1.0);

        var warning = tracker.RecordSample(55, 2 + ConnectionHealthTracker.PacketLossResendThreshold, 1.5);

        Assert.Equal(ConnectionWarningKind.PacketLoss, warning);
        Assert.Equal(ConnectionQuality.Unstable, tracker.Quality);
        Assert.Equal(ConnectionWarningKind.PacketLoss, tracker.ActiveWarning);
    }

    [Fact]
    public void High_jitter_flags_unstable_connection()
    {
        var tracker = new ConnectionHealthTracker();
        int[] pings = [35, 130, 40, 145, 30, 150];
        ConnectionWarningKind emitted = ConnectionWarningKind.None;

        for (int i = 0; i < pings.Length; i++)
        {
            var w = tracker.RecordSample(pings[i], 0, 1.0 + i * 0.5);
            if (w != ConnectionWarningKind.None)
            {
                emitted = w;
            }
        }

        Assert.Equal(ConnectionWarningKind.HighJitter, emitted);
        Assert.Equal(ConnectionQuality.Unstable, tracker.Quality);
        Assert.True(tracker.JitterMs >= ConnectionHealthTracker.HighJitterThresholdMs);
    }

    [Fact]
    public void Reset_clears_metrics_and_formats_empty_summary()
    {
        var tracker = new ConnectionHealthTracker();
        tracker.RecordSample(240, 10, 1.0);
        tracker.Reset();

        Assert.Equal(0, tracker.CurrentPingMs);
        Assert.Equal(0, tracker.PeakPingMs);
        Assert.Equal(ConnectionQuality.Good, tracker.Quality);
        Assert.Equal("no samples collected", tracker.FormatDiagnosticSummary());
    }
}
