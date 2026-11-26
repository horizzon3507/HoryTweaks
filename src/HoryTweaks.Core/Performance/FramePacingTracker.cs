namespace HoryTweaks.Core.Performance;

/// <summary>
/// Active frame-pacing warning surfaced to logs and the HUD when the main thread stalls or lags.
/// </summary>
internal enum FrameWarningKind
{
    None = 0,
    SustainedLowFps = 1,
    SevereHitch = 2
}

/// <summary>
/// Zero-allocation EMA frame-rate and main-thread hitch tracker.
/// Detects severe frame stalls (>= 100 ms) that risk Hazel UDP ping timeouts and sustained low FPS.
/// </summary>
internal sealed class FramePacingTracker
{
    internal const float HitchThresholdMs = 100f;
    internal const float LowFpsThreshold = 30f;
    internal const double LowFpsSustainSeconds = 3.0;
    internal const double WarningCooldownSeconds = 12.0;
    internal const double HitchBannerDurationSeconds = 5.0;

    private float _emaDeltaSeconds;
    private bool _initialized;
    private double _lowFpsSince = double.NegativeInfinity;
    private double _lastWarningTime = double.NegativeInfinity;
    private double _hitchBannerUntil = double.NegativeInfinity;

    internal float SmoothedFps => _emaDeltaSeconds > 0.0001f ? 1f / _emaDeltaSeconds : 0f;
    internal float LastFrameMs { get; private set; }
    internal float PeakFrameMs { get; private set; }
    internal int HitchCount { get; private set; }
    internal int LastHitchMs { get; private set; }
    internal FrameWarningKind ActiveWarning { get; private set; } = FrameWarningKind.None;

    /// <summary>
    /// Records one rendered frame's unscaled delta time (in seconds).
    /// Returns a non-<see cref="FrameWarningKind.None"/> value only when a new warning should be logged.
    /// </summary>
    internal FrameWarningKind RecordFrame(float unscaledDeltaSeconds, double nowSeconds)
    {
        if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds) || unscaledDeltaSeconds <= 0f)
        {
            return FrameWarningKind.None;
        }

        // Ignore extreme multi-second pauses (e.g., OS sleep or scene load) for EMA smoothing,
        // while still capping delta so SmoothedFps stays meaningful.
        float frameMs = unscaledDeltaSeconds * 1000f;
        LastFrameMs = frameMs;

        if (frameMs > PeakFrameMs && frameMs < 10_000f)
        {
            PeakFrameMs = frameMs;
        }

        float clampedDelta = Math.Min(unscaledDeltaSeconds, 0.5f);
        if (!_initialized)
        {
            _emaDeltaSeconds = clampedDelta;
            _initialized = true;
        }
        else
        {
            _emaDeltaSeconds = (_emaDeltaSeconds * 0.9f) + (clampedDelta * 0.1f);
        }

        bool isHitch = frameMs >= HitchThresholdMs && frameMs < 5000f;
        if (isHitch)
        {
            HitchCount++;
            LastHitchMs = (int)Math.Round(frameMs);
            _hitchBannerUntil = nowSeconds + HitchBannerDurationSeconds;
        }

        bool isLowFps = _initialized && SmoothedFps < LowFpsThreshold;
        if (isLowFps)
        {
            if (double.IsNegativeInfinity(_lowFpsSince))
            {
                _lowFpsSince = nowSeconds;
            }
        }
        else
        {
            _lowFpsSince = double.NegativeInfinity;
        }

        bool sustainedLowFps = !double.IsNegativeInfinity(_lowFpsSince)
            && (nowSeconds - _lowFpsSince >= LowFpsSustainSeconds);

        if (nowSeconds <= _hitchBannerUntil)
        {
            ActiveWarning = FrameWarningKind.SevereHitch;
        }
        else if (sustainedLowFps)
        {
            ActiveWarning = FrameWarningKind.SustainedLowFps;
        }
        else
        {
            ActiveWarning = FrameWarningKind.None;
        }

        if (isHitch && nowSeconds - _lastWarningTime >= WarningCooldownSeconds)
        {
            _lastWarningTime = nowSeconds;
            return FrameWarningKind.SevereHitch;
        }

        if (sustainedLowFps && nowSeconds - _lastWarningTime >= WarningCooldownSeconds)
        {
            _lastWarningTime = nowSeconds;
            return FrameWarningKind.SustainedLowFps;
        }

        return FrameWarningKind.None;
    }

    internal void Reset()
    {
        _emaDeltaSeconds = 0f;
        _initialized = false;
        _lowFpsSince = double.NegativeInfinity;
        _lastWarningTime = double.NegativeInfinity;
        _hitchBannerUntil = double.NegativeInfinity;
        LastFrameMs = 0f;
        PeakFrameMs = 0f;
        HitchCount = 0;
        LastHitchMs = 0;
        ActiveWarning = FrameWarningKind.None;
    }

    internal string FormatDiagnosticSummary()
    {
        if (!_initialized)
        {
            return "no frames sampled";
        }

        return $"avgFps={SmoothedFps:F0}, lastFrame={LastFrameMs:F1}ms, peakFrame={PeakFrameMs:F0}ms, hitches={HitchCount}";
    }
}
