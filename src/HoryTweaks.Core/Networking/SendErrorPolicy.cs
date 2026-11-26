namespace HoryTweaks.Core.Networking;

/// <summary>
/// Mirrors Hazel.SendErrors values so send-error decisions stay Unity/IL2CPP-independent.
/// </summary>
internal enum SendErrorKind
{
    None = 0,
    Disconnected = 1,
    Unknown = 2
}

/// <summary>
/// Outcome returned by <see cref="SendErrorTracker"/> after attempting to send a packet.
/// </summary>
internal enum SendErrorAction
{
    /// <summary>Packet succeeded; continue normally.</summary>
    Continue = 0,

    /// <summary>Transient send failure absorbed without tearing down the session.</summary>
    IgnoreTransient = 1,

    /// <summary>Fatal or sustained failure; enqueue a disconnect.</summary>
    Disconnect = 2
}

/// <summary>
/// Tracks consecutive send errors inside a rolling time window so a single dropped unreliable
/// movement packet (e.g., transient socket congestion or Wine/Proton EWOULDBLOCK) does not
/// immediately kick the player out of an active lobby or match.
/// </summary>
internal sealed class SendErrorTracker
{
    /// <summary>Consecutive unreliable unknown send errors within the window before disconnecting.</summary>
    internal const int MaxConsecutiveUnreliableErrors = 5;

    /// <summary>Consecutive reliable unknown send errors within the window before disconnecting.</summary>
    internal const int MaxConsecutiveReliableErrors = 2;

    /// <summary>Window in seconds during which consecutive transient errors accumulate.</summary>
    internal const double ErrorWindowSeconds = 5.0;

    /// <summary>Minimum seconds between warning log messages for absorbed transient errors.</summary>
    internal const double WarningCooldownSeconds = 3.0;

    private int _consecutiveUnreliableErrors;
    private int _consecutiveReliableErrors;
    private double _lastErrorTime = double.NegativeInfinity;
    private double _lastWarningTime = double.NegativeInfinity;

    /// <summary>Total number of transient send errors absorbed since the last reset.</summary>
    internal int AbsorbedTransientErrors { get; private set; }

    /// <summary>
    /// Evaluates a send result and returns whether to continue, absorb as transient, or disconnect.
    /// </summary>
    internal SendErrorAction Evaluate(
        SendErrorKind error,
        bool isReliable,
        double nowSeconds,
        out bool shouldLogWarning)
    {
        shouldLogWarning = false;

        if (error == SendErrorKind.None)
        {
            _consecutiveUnreliableErrors = 0;
            _consecutiveReliableErrors = 0;
            return SendErrorAction.Continue;
        }

        if (error == SendErrorKind.Disconnected)
        {
            _consecutiveUnreliableErrors = 0;
            _consecutiveReliableErrors = 0;
            return SendErrorAction.Disconnect;
        }

        if (nowSeconds - _lastErrorTime > ErrorWindowSeconds)
        {
            _consecutiveUnreliableErrors = 0;
            _consecutiveReliableErrors = 0;
        }

        _lastErrorTime = nowSeconds;

        if (isReliable)
        {
            _consecutiveReliableErrors++;
            if (_consecutiveReliableErrors >= MaxConsecutiveReliableErrors)
            {
                return SendErrorAction.Disconnect;
            }
        }
        else
        {
            _consecutiveUnreliableErrors++;
            if (_consecutiveUnreliableErrors >= MaxConsecutiveUnreliableErrors)
            {
                return SendErrorAction.Disconnect;
            }
        }

        AbsorbedTransientErrors++;

        if (nowSeconds - _lastWarningTime >= WarningCooldownSeconds)
        {
            _lastWarningTime = nowSeconds;
            shouldLogWarning = true;
        }

        return SendErrorAction.IgnoreTransient;
    }

    /// <summary>Resets counters when starting or leaving a game session.</summary>
    internal void Reset()
    {
        _consecutiveUnreliableErrors = 0;
        _consecutiveReliableErrors = 0;
        _lastErrorTime = double.NegativeInfinity;
        _lastWarningTime = double.NegativeInfinity;
        AbsorbedTransientErrors = 0;
    }
}
