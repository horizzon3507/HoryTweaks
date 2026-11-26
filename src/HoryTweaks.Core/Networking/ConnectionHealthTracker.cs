namespace HoryTweaks.Core.Networking;

/// <summary>
/// Overall connection quality tier derived from ping, jitter, and reliable packet resends.
/// </summary>
internal enum ConnectionQuality
{
    Good = 0,
    Fair = 1,
    HighPing = 2,
    Unstable = 3
}

/// <summary>
/// Active warning category surfaced to logs and the HUD when connection health degrades.
/// </summary>
internal enum ConnectionWarningKind
{
    None = 0,
    HighPing = 1,
    HighJitter = 2,
    PacketLoss = 3
}

/// <summary>
/// Zero-allocation rolling tracker for ping, jitter, and packet resend spikes.
/// Uses hysteresis and cooldowns so degradation warnings stay accurate and never spam.
/// </summary>
internal sealed class ConnectionHealthTracker
{
    internal const int WindowSize = 16;
    internal const int FairPingThresholdMs = 110;
    internal const int HighPingThresholdMs = 180;
    internal const int HighJitterThresholdMs = 45;
    internal const int PacketLossResendThreshold = 4;
    internal const int SustainedSamplesRequired = 3;
    internal const double WarningCooldownSeconds = 15.0;

    private readonly int[] _pingSamples = new int[WindowSize];
    private int _sampleCount;
    private int _writeIndex;
    private int _lastRawPing = -1;
    private int _lastTotalResends = -1;
    private int _highPingStreak;
    private int _highJitterStreak;
    private double _lastWarningTime = double.NegativeInfinity;
    private ConnectionWarningKind _lastWarningEmitted = ConnectionWarningKind.None;

    internal int CurrentPingMs { get; private set; }
    internal float SmoothedPingMs { get; private set; }
    internal float JitterMs { get; private set; }
    internal int PeakPingMs { get; private set; }
    internal int RecentResends { get; private set; }
    internal int TotalResendsObserved { get; private set; }
    internal int WarningCount { get; private set; }
    internal ConnectionQuality Quality { get; private set; } = ConnectionQuality.Good;
    internal ConnectionWarningKind ActiveWarning { get; private set; } = ConnectionWarningKind.None;

    /// <summary>
    /// Records a new sample of current RTT ping (ms) and cumulative Hazel resend count.
    /// Returns a non-<see cref="ConnectionWarningKind.None"/> value only when a new or
    /// cooldown-expired warning should be surfaced to the player/log.
    /// </summary>
    internal ConnectionWarningKind RecordSample(int pingMs, int totalResends, double nowSeconds)
    {
        int clampedPing = Math.Max(0, pingMs);
        int clampedResends = Math.Max(0, totalResends);

        CurrentPingMs = clampedPing;
        if (clampedPing > PeakPingMs)
        {
            PeakPingMs = clampedPing;
        }

        if (_sampleCount == 0)
        {
            SmoothedPingMs = clampedPing;
        }
        else
        {
            SmoothedPingMs = (SmoothedPingMs * 0.7f) + (clampedPing * 0.3f);
        }

        _pingSamples[_writeIndex] = clampedPing;
        _writeIndex = (_writeIndex + 1) % WindowSize;
        if (_sampleCount < WindowSize)
        {
            _sampleCount++;
        }

        JitterMs = ComputeJitter();

        int resendDelta = 0;
        if (_lastTotalResends >= 0 && clampedResends >= _lastTotalResends)
        {
            resendDelta = clampedResends - _lastTotalResends;
        }

        _lastTotalResends = clampedResends;
        TotalResendsObserved = clampedResends;
        RecentResends = resendDelta;
        _lastRawPing = clampedPing;

        _highPingStreak = clampedPing >= HighPingThresholdMs ? _highPingStreak + 1 : 0;
        _highJitterStreak = JitterMs >= HighJitterThresholdMs ? _highJitterStreak + 1 : 0;

        ConnectionWarningKind currentWarning = DetermineWarning(resendDelta);
        ActiveWarning = currentWarning;
        Quality = DetermineQuality(clampedPing, currentWarning);

        if (currentWarning == ConnectionWarningKind.None)
        {
            return ConnectionWarningKind.None;
        }

        bool cooldownElapsed = nowSeconds - _lastWarningTime >= WarningCooldownSeconds;
        bool escalated = currentWarning != _lastWarningEmitted && nowSeconds - _lastWarningTime >= 4.0;
        if (cooldownElapsed || escalated)
        {
            _lastWarningTime = nowSeconds;
            _lastWarningEmitted = currentWarning;
            WarningCount++;
            return currentWarning;
        }

        return ConnectionWarningKind.None;
    }

    /// <summary>Resets session telemetry when connecting to a new lobby or disconnecting.</summary>
    internal void Reset()
    {
        Array.Clear(_pingSamples, 0, _pingSamples.Length);
        _sampleCount = 0;
        _writeIndex = 0;
        _lastRawPing = -1;
        _lastTotalResends = -1;
        _highPingStreak = 0;
        _highJitterStreak = 0;
        _lastWarningTime = double.NegativeInfinity;
        _lastWarningEmitted = ConnectionWarningKind.None;
        CurrentPingMs = 0;
        SmoothedPingMs = 0f;
        JitterMs = 0f;
        PeakPingMs = 0;
        RecentResends = 0;
        TotalResendsObserved = 0;
        WarningCount = 0;
        Quality = ConnectionQuality.Good;
        ActiveWarning = ConnectionWarningKind.None;
    }

    /// <summary>Builds a concise diagnostic line suitable for /dump reports.</summary>
    internal string FormatDiagnosticSummary()
    {
        if (_sampleCount == 0)
        {
            return "no samples collected";
        }

        return $"ping={CurrentPingMs}ms (avg={SmoothedPingMs:F0}ms, peak={PeakPingMs}ms, jitter={JitterMs:F0}ms), resends={TotalResendsObserved}, quality={Quality}, warnings={WarningCount}";
    }

    private float ComputeJitter()
    {
        if (_sampleCount < 2)
        {
            return 0f;
        }

        int oldestIndex = _sampleCount < WindowSize ? 0 : _writeIndex;
        int sumAbsDiff = 0;
        int prev = _pingSamples[oldestIndex];

        for (int i = 1; i < _sampleCount; i++)
        {
            int idx = (oldestIndex + i) % WindowSize;
            int current = _pingSamples[idx];
            sumAbsDiff += Math.Abs(current - prev);
            prev = current;
        }

        return (float)sumAbsDiff / (_sampleCount - 1);
    }

    private ConnectionWarningKind DetermineWarning(int resendDelta)
    {
        if (resendDelta >= PacketLossResendThreshold)
        {
            return ConnectionWarningKind.PacketLoss;
        }

        if (_highJitterStreak >= SustainedSamplesRequired)
        {
            return ConnectionWarningKind.HighJitter;
        }

        if (_highPingStreak >= SustainedSamplesRequired)
        {
            return ConnectionWarningKind.HighPing;
        }

        return ConnectionWarningKind.None;
    }

    private ConnectionQuality DetermineQuality(int pingMs, ConnectionWarningKind warning)
    {
        if (warning == ConnectionWarningKind.PacketLoss || warning == ConnectionWarningKind.HighJitter)
        {
            return ConnectionQuality.Unstable;
        }

        if (warning == ConnectionWarningKind.HighPing || pingMs >= HighPingThresholdMs)
        {
            return ConnectionQuality.HighPing;
        }

        if (pingMs >= FairPingThresholdMs || JitterMs >= HighJitterThresholdMs * 0.6f)
        {
            return ConnectionQuality.Fair;
        }

        return ConnectionQuality.Good;
    }
}
