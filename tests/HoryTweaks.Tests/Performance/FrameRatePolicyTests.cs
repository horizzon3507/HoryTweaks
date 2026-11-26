using HoryTweaks.Core.Performance;
using Xunit;

namespace HoryTweaks.Tests.Performance;

public class FrameRatePolicyTests
{
    [Fact]
    public void Locked_mode_always_returns_60_fps_without_warnings()
    {
        var decision = FrameRatePolicy.Compute(unlockFps: false, monitorRefreshHz: 165, vSyncEnabled: true);

        Assert.Equal(60, decision.TargetFps);
        Assert.Equal(165, decision.DetectedRefreshHz);
        Assert.Equal(FrameRateWarningKind.None, decision.Warning);
    }

    [Theory]
    [InlineData(60, 120)]
    [InlineData(75, 150)]
    [InlineData(120, 120)]
    [InlineData(144, 144)]
    [InlineData(165, 165)]
    [InlineData(240, 240)]
    public void Unlocked_mode_scales_cleanly_within_120_to_240_band(double refreshHz, int expectedFps)
    {
        var decision = FrameRatePolicy.Compute(unlockFps: true, monitorRefreshHz: refreshHz, vSyncEnabled: false);

        Assert.Equal(expectedFps, decision.TargetFps);
        Assert.Equal(FrameRateWarningKind.None, decision.Warning);
    }

    [Fact]
    public void Ultra_high_refresh_monitor_clamps_to_240_with_warning()
    {
        var decision = FrameRatePolicy.Compute(unlockFps: true, monitorRefreshHz: 360, vSyncEnabled: false);

        Assert.Equal(FrameRatePolicy.MaxUnlockedFps, decision.TargetFps);
        Assert.Equal(FrameRateWarningKind.ClampedToSafeCap, decision.Warning);
    }

    [Fact]
    public void Unknown_refresh_rate_falls_back_to_120_with_warning()
    {
        var decision = FrameRatePolicy.Compute(unlockFps: true, monitorRefreshHz: 0, vSyncEnabled: false);

        Assert.Equal(FrameRatePolicy.MinUnlockedFps, decision.TargetFps);
        Assert.Equal(0, decision.DetectedRefreshHz);
        Assert.Equal(FrameRateWarningKind.RefreshRateUnknown, decision.Warning);
    }

    [Fact]
    public void Enabled_vsync_with_unlock_fps_reports_conflict_warning()
    {
        var decision = FrameRatePolicy.Compute(unlockFps: true, monitorRefreshHz: 144, vSyncEnabled: true);

        Assert.Equal(144, decision.TargetFps);
        Assert.Equal(FrameRateWarningKind.VSyncConflict, decision.Warning);
    }
}
