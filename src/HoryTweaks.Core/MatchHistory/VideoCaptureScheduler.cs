namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Reason video capture stopped itself to protect gameplay smoothness.
/// </summary>
internal enum VideoCaptureStopReason
{
    None = 0,
    ReadbackUnsupported = 1,
    MainThreadBudgetExceeded = 2,
    SustainedLowFps = 3,
    ReadbackError = 4
}

/// <summary>
/// Pure, allocation-free state machine governing video capture pacing and main-thread frame-budget
/// protection. Steps capture FPS down when capture work or game FPS degrade, steps back up after a
/// sustained healthy window, and stops entirely when even minimum-FPS capture keeps hurting the game.
/// </summary>
internal sealed class VideoCaptureScheduler
{
    private const double FastRecoveryWorkMs = 4.0;
    private const double FastRecoveryMinGameFps = 40.0;
    private const double FastRecoverySustainSeconds = 10.0;
    private const int SlowStreakBeforeStepDown = 3;
    private const int FpsStep = 5;

    private readonly int _targetFps;
    private readonly int _minFps;

    private double _lowFpsSinceSec = double.NegativeInfinity;
    private double _fastSinceSec = double.NegativeInfinity;
    private double _emaDeltaSec;
    private bool _emaInitialized;

    internal int CurrentFps { get; private set; }
    internal double NextCaptureTimeSec { get; private set; }
    internal int SlowCaptureStreak { get; private set; }
    internal VideoCaptureStopReason StopReason { get; private set; } = VideoCaptureStopReason.None;
    internal bool IsStopped => StopReason != VideoCaptureStopReason.None;

    internal VideoCaptureScheduler(int targetFps = MatchVideoRules.TargetFps, int minFps = MatchVideoRules.MinAdaptiveFps)
    {
        _targetFps = Math.Max(targetFps, 1);
        _minFps = Math.Max(minFps, 1);
        CurrentFps = _targetFps;
    }

    internal void Reset()
    {
        CurrentFps = _targetFps;
        NextCaptureTimeSec = 0;
        SlowCaptureStreak = 0;
        _lowFpsSinceSec = double.NegativeInfinity;
        _fastSinceSec = double.NegativeInfinity;
        _emaDeltaSec = 0;
        _emaInitialized = false;
        StopReason = VideoCaptureStopReason.None;
    }

    /// <summary>
    /// Returns true when a new capture may be scheduled: capture is running, the encoder is ready,
    /// fewer than <paramref name="maxInFlight"/> readbacks are in flight and the next capture is due.
    /// </summary>
    internal bool ShouldStartCapture(double nowSec, bool encoderReady, int inFlightCount, int maxInFlight)
    {
        if (IsStopped || !encoderReady || inFlightCount >= maxInFlight || nowSec < NextCaptureTimeSec)
            return false;

        return true;
    }

    /// <summary>
    /// Schedules the next capture strictly forward from now. Never catches up on missed frames,
    /// so a stall can never trigger a death spiral of back-to-back captures.
    /// </summary>
    internal void RecordCaptureScheduled(double nowSec)
    {
        NextCaptureTimeSec = nowSec + (1.0 / Math.Max(CurrentFps, 1));
    }

    /// <summary>
    /// Feeds the measured main-thread cost of capture work and the game's frame delta.
    /// Returns a non-<see cref="VideoCaptureStopReason.None"/> reason the moment capture must stop.
    /// </summary>
    internal VideoCaptureStopReason RecordMainThreadWork(double workMs, double frameDeltaSec, double nowSec)
    {
        if (IsStopped)
            return StopReason;

        if (frameDeltaSec > 0 && !double.IsNaN(frameDeltaSec) && !double.IsInfinity(frameDeltaSec))
        {
            double clamped = Math.Clamp(frameDeltaSec, 0.001, 0.5);
            if (!_emaInitialized)
            {
                _emaDeltaSec = clamped;
                _emaInitialized = true;
            }
            else
            {
                _emaDeltaSec = (_emaDeltaSec * 0.9) + (clamped * 0.1);
            }
        }

        // Main-thread capture work budget.
        if (workMs > MatchVideoRules.MaxMainThreadCaptureMs)
        {
            SlowCaptureStreak++;
            _fastSinceSec = double.NegativeInfinity;

            if (SlowCaptureStreak >= SlowStreakBeforeStepDown && CurrentFps > _minFps)
            {
                CurrentFps = Math.Max(_minFps, CurrentFps - FpsStep);
            }

            if (SlowCaptureStreak >= MatchVideoRules.AutoPauseConsecutiveSlowCaptures && CurrentFps <= _minFps)
            {
                StopReason = VideoCaptureStopReason.MainThreadBudgetExceeded;
                return StopReason;
            }
        }
        else
        {
            SlowCaptureStreak = Math.Max(0, SlowCaptureStreak - 1);
        }

        // Sustained low game FPS while capturing.
        if (_emaInitialized && (1.0 / _emaDeltaSec) < MatchVideoRules.AutoPauseLowFpsThreshold)
        {
            _fastSinceSec = double.NegativeInfinity;

            if (double.IsNegativeInfinity(_lowFpsSinceSec))
            {
                _lowFpsSinceSec = nowSec;
            }
            else if (nowSec - _lowFpsSinceSec >= MatchVideoRules.AutoPauseLowFpsDurationSeconds)
            {
                if (CurrentFps > _minFps)
                {
                    CurrentFps = Math.Max(_minFps, CurrentFps - FpsStep);
                    _lowFpsSinceSec = nowSec;
                }
                else
                {
                    StopReason = VideoCaptureStopReason.SustainedLowFps;
                    return StopReason;
                }
            }
        }
        else
        {
            _lowFpsSinceSec = double.NegativeInfinity;

            if (CurrentFps < _targetFps && workMs <= FastRecoveryWorkMs && (!_emaInitialized || (1.0 / _emaDeltaSec) >= FastRecoveryMinGameFps))
            {
                if (double.IsNegativeInfinity(_fastSinceSec))
                {
                    _fastSinceSec = nowSec;
                }
                else if (nowSec - _fastSinceSec >= FastRecoverySustainSeconds)
                {
                    CurrentFps = Math.Min(_targetFps, CurrentFps + FpsStep);
                    _fastSinceSec = nowSec;
                }
            }
            else
            {
                // Any non-healthy frame — including 20-39 FPS game play even when capture work is
                // cheap — resets the recovery window so FPS never steps up into a degraded game.
                _fastSinceSec = double.NegativeInfinity;
            }
        }

        return VideoCaptureStopReason.None;
    }
}
