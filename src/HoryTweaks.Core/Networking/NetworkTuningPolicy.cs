namespace HoryTweaks.Core.Networking;

/// <summary>
/// Computed client-side Hazel UDP and suspension tolerances applied to an active connection.
/// </summary>
internal readonly record struct TunedConnectionSettings(
    int DisconnectTimeoutMs,
    int ResendLimit,
    float ResendPingMultiplier,
    int SecondsSuspendedBeforeDisconnect
);

/// <summary>
/// Pure, vanilla-server-compatible tuning targets that prevent premature client-side
/// disconnects from micro-stalls, Wi-Fi jitter, map loads, and brief Alt+Tab pauses.
/// </summary>
internal static class NetworkTuningPolicy
{
    /// <summary>Client-side disconnect timeout in milliseconds (vanilla defaults around 5000 ms).</summary>
    internal const int TargetDisconnectTimeoutMs = 12000;

    /// <summary>
    /// Minimum finite resend attempts for a reliable packet before disconnecting (vanilla defaults around 6).
    /// Zero in Hazel means unlimited retries (bounded only by DisconnectTimeoutMs) and is always preserved.
    /// </summary>
    internal const int TargetResendLimit = 10;

    /// <summary>RTT multiplier for reliable resend spacing; prevents resend storms on jittery Wi-Fi.</summary>
    internal const float TargetResendPingMultiplier = 1.6f;

    /// <summary>Seconds the client may stay suspended/unfocused before disconnecting locally.</summary>
    internal const int TargetSecondsSuspendedBeforeDisconnect = 30;

    /// <summary>
    /// Computes safe tuned values without ever weakening a connection that already uses
    /// larger timeouts, unlimited retries (ResendLimit == 0), or higher retry limits.
    /// Note: Hazel's UnityUdpClientConnection.ReceiveBufferSize is intentionally NOT altered
    /// because Hazel uses it as a per-datagram MessageReader.GetSized allocation size, not an OS queue.
    /// </summary>
    internal static TunedConnectionSettings ComputeTunedSettings(
        int currentDisconnectTimeoutMs,
        int currentResendLimit,
        float currentResendPingMultiplier,
        int currentSecondsSuspended)
    {
        // Hazel's KeepAliveInterval and ResendTimeoutMs are intentionally preserved:
        // mutating KeepAliveInterval restarts Hazel's keepAliveTimer mid-connection, and
        // overwriting ResendTimeoutMs can corrupt adaptive resend behavior.
        return new TunedConnectionSettings(
            DisconnectTimeoutMs: Math.Max(currentDisconnectTimeoutMs, TargetDisconnectTimeoutMs),
            ResendLimit: currentResendLimit == 0 ? 0 : Math.Max(currentResendLimit, TargetResendLimit),
            ResendPingMultiplier: Math.Max(currentResendPingMultiplier, TargetResendPingMultiplier),
            SecondsSuspendedBeforeDisconnect: Math.Max(currentSecondsSuspended, TargetSecondsSuspendedBeforeDisconnect));
    }
}
