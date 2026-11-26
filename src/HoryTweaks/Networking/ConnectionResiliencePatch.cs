using HarmonyLib;
using Hazel.Udp;
using HoryTweaks.Core.Networking;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Configuration;
using InnerNet;
using UnityEngine;

namespace HoryTweaks.Networking;

/// <summary>
/// Applies safe, vanilla-server-compatible Hazel UDP and background-suspend tolerances,
/// preserves original connection baselines for clean restoration on toggle-off/unload,
/// and tracks live connection health (ping, jitter, reliable resend spikes).
/// </summary>
[HarmonyPatch]
internal static class ConnectionResiliencePatch
{
    private const float SampleIntervalSeconds = 0.5f;

    private readonly record struct UdpBaseline(
        nint ConnectionPtr,
        int DisconnectTimeoutMs,
        int ResendTimeoutMs,
        int ResendLimit,
        float ResendPingMultiplier
    );

    internal static readonly ConnectionHealthTracker HealthTracker = new();

    /// <summary>
    /// Snapshot captured right before disconnect reset so /dump still reflects the ended session.
    /// </summary>
    internal static string LastEndedSessionSummary { get; private set; } = "none";

    private static int? _originalSecondsSuspended;
    private static UdpBaseline? _currentUdpBaseline;
    private static nint _lastObservedConnectionPtr;
    private static bool _tuningAppliedToCurrentConnection;
    private static float _nextSampleTime;

    /// <summary>
    /// Applies or restores connection tuning immediately on the active <see cref="AmongUsClient"/>.
    /// </summary>
    internal static void ApplyToActiveClient()
    {
        ApplyClientSettings();

        var client = AmongUsClient.Instance;
        if (client == null || client.connection == null)
            return;

        var udp = client.connection.TryCast<UdpConnection>();
        if (udp == null)
            return;

        if (BAUConfigs.NetworkOptimization.Value)
        {
            ApplyUdpSettings(udp);
        }
        else
        {
            RestoreUdpSettings(udp);
        }
    }

    /// <summary>
    /// Restores vanilla suspension and active UDP values when switching to vanilla / unloading the mod.
    /// </summary>
    internal static void RestoreOnUnload()
    {
        if (_originalSecondsSuspended.HasValue)
        {
            InnerNetClient.SecondsSuspendedBeforeDisconnect = _originalSecondsSuspended.Value;
        }

        var client = AmongUsClient.Instance;
        if (client != null && client.connection != null)
        {
            var udp = client.connection.TryCast<UdpConnection>();
            if (udp != null)
            {
                RestoreUdpSettings(udp);
            }
        }
    }

    [HarmonyPatch(typeof(UnityUdpClientConnection), nameof(UnityUdpClientConnection.ConnectAsync))]
    [HarmonyPrefix]
    private static void UnityUdpClientConnection_ConnectAsync_Prefix(UnityUdpClientConnection __instance)
    {
        if (__instance == null)
            return;

        CaptureBaselineIfNew(__instance);
        if (BAUConfigs.NetworkOptimization.Value)
        {
            ApplyUdpSettings(__instance);
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Start))]
    [HarmonyPostfix]
    private static void InnerNetClient_Start_Postfix(InnerNetClient __instance)
    {
        if (__instance == null)
            return;

        ApplyClientSettings();
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Connect))]
    [HarmonyPostfix]
    private static void InnerNetClient_Connect_Postfix(InnerNetClient __instance)
    {
        if (__instance == null)
            return;

        _lastObservedConnectionPtr = 0;
        _tuningAppliedToCurrentConnection = false;
        _currentUdpBaseline = null;
        HealthTracker.Reset();
        NetworkManager.ResetSession();
        ApplyClientSettings();

        if (__instance.connection != null)
        {
            var udp = __instance.connection.TryCast<UdpConnection>();
            if (udp != null)
            {
                CaptureBaselineIfNew(udp);
                if (BAUConfigs.NetworkOptimization.Value)
                {
                    ApplyUdpSettings(udp);
                }
            }
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Update))]
    [HarmonyPostfix]
    private static void InnerNetClient_Update_Postfix(InnerNetClient __instance)
    {
        if (__instance == null)
            return;

        if (__instance.connection != null)
        {
            nint connPtr = __instance.connection.Pointer;
            if (connPtr != _lastObservedConnectionPtr)
            {
                _lastObservedConnectionPtr = connPtr;
                _tuningAppliedToCurrentConnection = false;
                var udp = __instance.connection.TryCast<UdpConnection>();
                if (udp != null)
                {
                    CaptureBaselineIfNew(udp);
                    if (BAUConfigs.NetworkOptimization.Value)
                    {
                        ApplyUdpSettings(udp);
                    }
                }
            }
        }

        if (!__instance.AmConnected || GameState.IsFreePlay)
            return;

        float now = Time.unscaledTime;
        if (now < _nextSampleTime)
            return;

        _nextSampleTime = now + SampleIntervalSeconds;

        int ping = __instance.Ping;
        int resends = __instance.Resends;
        var warning = HealthTracker.RecordSample(ping, resends, Time.unscaledTimeAsDouble);

        if (warning != ConnectionWarningKind.None)
        {
            LogConnectionWarning(warning);
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    [HarmonyPostfix]
    private static void InnerNetClient_DisconnectInternal_Postfix(DisconnectReasons reason)
    {
        _lastObservedConnectionPtr = 0;
        _tuningAppliedToCurrentConnection = false;
        _currentUdpBaseline = null;

        string reasonName = Enum.GetName(reason) ?? ((int)reason).ToString();
        string healthSummary = HealthTracker.FormatDiagnosticSummary();
        string dispatchSummary = NetworkManager.FormatDiagnosticSummary();
        LastEndedSessionSummary = $"reason={reasonName}; {healthSummary}; {dispatchSummary}";

        if (reason != DisconnectReasons.ExitGame && reason != DisconnectReasons.Destroy)
        {
            BAUPlugin.Logger.Warning(
                $"Disconnected ({reasonName}). Health: {healthSummary} | Dispatch: {dispatchSummary}",
                "NetworkHealth");
        }

        HealthTracker.Reset();
        NetworkManager.ResetSession();
    }

    private static void CaptureOriginalSuspendIfNeeded()
    {
        if (!_originalSecondsSuspended.HasValue)
        {
            _originalSecondsSuspended = InnerNetClient.SecondsSuspendedBeforeDisconnect;
        }
    }

    private static void CaptureBaselineIfNew(UdpConnection udp)
    {
        nint ptr = udp.Pointer;
        if (_currentUdpBaseline.HasValue && _currentUdpBaseline.Value.ConnectionPtr == ptr)
            return;

        _currentUdpBaseline = new UdpBaseline(
            ConnectionPtr: ptr,
            DisconnectTimeoutMs: udp.DisconnectTimeoutMs,
            ResendTimeoutMs: udp.ResendTimeoutMs,
            ResendLimit: udp.ResendLimit,
            ResendPingMultiplier: udp.ResendPingMultiplier);
    }

    private static void ApplyClientSettings()
    {
        CaptureOriginalSuspendIfNeeded();

        if (BAUConfigs.NetworkOptimization.Value)
        {
            InnerNetClient.SecondsSuspendedBeforeDisconnect = Math.Max(
                _originalSecondsSuspended!.Value,
                NetworkTuningPolicy.TargetSecondsSuspendedBeforeDisconnect);
        }
        else if (_originalSecondsSuspended.HasValue)
        {
            InnerNetClient.SecondsSuspendedBeforeDisconnect = _originalSecondsSuspended.Value;
        }
    }

    private static void ApplyUdpSettings(UdpConnection udp)
    {
        CaptureOriginalSuspendIfNeeded();
        CaptureBaselineIfNew(udp);

        if (_tuningAppliedToCurrentConnection && _lastObservedConnectionPtr == udp.Pointer)
            return;

        var baseline = _currentUdpBaseline!.Value;
        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            baseline.DisconnectTimeoutMs,
            baseline.ResendLimit,
            baseline.ResendPingMultiplier,
            _originalSecondsSuspended!.Value);

        udp.KeepAliveInterval = tuned.KeepAliveIntervalMs;
        udp.DisconnectTimeoutMs = tuned.DisconnectTimeoutMs;
        udp.ResendTimeoutMs = tuned.ResendTimeoutMs;
        udp.ResendLimit = tuned.ResendLimit;
        udp.ResendPingMultiplier = tuned.ResendPingMultiplier;

        InnerNetClient.SecondsSuspendedBeforeDisconnect = tuned.SecondsSuspendedBeforeDisconnect;
        _lastObservedConnectionPtr = udp.Pointer;
        _tuningAppliedToCurrentConnection = true;

        BAUPlugin.Logger.Log(
            $"Applied Hazel UDP tuning (keepAlive={tuned.KeepAliveIntervalMs}ms, timeout={tuned.DisconnectTimeoutMs}ms, resendLimit={tuned.ResendLimit}, resendMult={tuned.ResendPingMultiplier:F1}, suspend={tuned.SecondsSuspendedBeforeDisconnect}s)",
            "NetworkTuning");
    }

    private static void RestoreUdpSettings(UdpConnection udp)
    {
        if (!_currentUdpBaseline.HasValue || _currentUdpBaseline.Value.ConnectionPtr != udp.Pointer)
            return;

        var baseline = _currentUdpBaseline.Value;
        udp.DisconnectTimeoutMs = baseline.DisconnectTimeoutMs;
        udp.ResendTimeoutMs = baseline.ResendTimeoutMs;
        udp.ResendLimit = baseline.ResendLimit;
        udp.ResendPingMultiplier = baseline.ResendPingMultiplier;
        _tuningAppliedToCurrentConnection = false;

        BAUPlugin.Logger.Log("Restored original Hazel UDP settings for active connection.", "NetworkTuning");
    }

    private static void LogConnectionWarning(ConnectionWarningKind warning)
    {
        switch (warning)
        {
            case ConnectionWarningKind.HighPing:
                BAUPlugin.Logger.Warning(
                    $"High ping detected ({HealthTracker.CurrentPingMs}ms, avg {HealthTracker.SmoothedPingMs:F0}ms, jitter ±{HealthTracker.JitterMs:F0}ms).",
                    "NetworkHealth");
                break;

            case ConnectionWarningKind.HighJitter:
                BAUPlugin.Logger.Warning(
                    $"Unstable latency / high jitter detected (±{HealthTracker.JitterMs:F0}ms, current ping {HealthTracker.CurrentPingMs}ms).",
                    "NetworkHealth");
                break;

            case ConnectionWarningKind.PacketLoss:
                BAUPlugin.Logger.Warning(
                    $"Packet loss / resend spike detected (+{HealthTracker.RecentResends} resends, total {HealthTracker.TotalResendsObserved}, ping {HealthTracker.CurrentPingMs}ms).",
                    "NetworkHealth");
                break;
        }
    }
}
