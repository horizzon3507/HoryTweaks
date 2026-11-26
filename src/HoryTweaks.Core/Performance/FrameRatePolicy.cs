namespace HoryTweaks.Core.Performance;

/// <summary>
/// Advisory warning generated when computing the target frame rate for UnlockFPS.
/// </summary>
internal enum FrameRateWarningKind
{
    None = 0,
    RefreshRateUnknown = 1,
    ClampedToSafeCap = 2,
    VSyncConflict = 3
}

/// <summary>
/// Result of computing the target frame rate from user settings, monitor refresh rate, and VSync state.
/// </summary>
internal readonly record struct FrameRateDecision(
    int TargetFps,
    int DetectedRefreshHz,
    FrameRateWarningKind Warning
);

/// <summary>
/// Pure policy for choosing a smooth, network-safe target frame rate instead of spinning at 999 FPS.
/// Keeping the target between 120 and 240 FPS preserves high-refresh fluidity while avoiding 100%
/// CPU/GPU contention that starves Hazel's UDP thread and causes ping spikes under Windows or Proton.
/// </summary>
internal static class FrameRatePolicy
{
    internal const int DefaultLockedFps = 60;
    internal const int MinUnlockedFps = 120;
    internal const int MaxUnlockedFps = 240;
    internal const int FallbackRefreshHz = 60;

    /// <summary>
    /// Computes the target FPS cap and any diagnostic warning to surface to the player/log.
    /// </summary>
    internal static FrameRateDecision Compute(bool unlockFps, double monitorRefreshHz, bool vSyncEnabled)
    {
        int roundedHz = NormalizeRefreshRate(monitorRefreshHz);

        if (!unlockFps)
        {
            return new FrameRateDecision(DefaultLockedFps, roundedHz, FrameRateWarningKind.None);
        }

        bool unknownRefresh = roundedHz <= 0;
        int effectiveHz = unknownRefresh ? FallbackRefreshHz : roundedHz;

        // For 60-119 Hz panels, 2x refresh (clamped 120..240) reduces input latency without runaway CPU spin.
        // For 120-240 Hz panels, match refresh rate directly. Above 240 Hz, clamp to 240 FPS safely.
        int desired = effectiveHz < MinUnlockedFps ? effectiveHz * 2 : effectiveHz;
        bool wasClamped = desired > MaxUnlockedFps;
        int targetFps = Math.Clamp(desired, MinUnlockedFps, MaxUnlockedFps);

        FrameRateWarningKind warning = FrameRateWarningKind.None;
        if (vSyncEnabled)
        {
            warning = FrameRateWarningKind.VSyncConflict;
        }
        else if (wasClamped)
        {
            warning = FrameRateWarningKind.ClampedToSafeCap;
        }
        else if (unknownRefresh)
        {
            warning = FrameRateWarningKind.RefreshRateUnknown;
        }

        return new FrameRateDecision(targetFps, unknownRefresh ? 0 : effectiveHz, warning);
    }

    private static int NormalizeRefreshRate(double refreshHz)
    {
        if (double.IsNaN(refreshHz) || double.IsInfinity(refreshHz) || refreshHz < 24.0 || refreshHz > 1000.0)
        {
            return 0;
        }

        return (int)Math.Round(refreshHz);
    }
}
