using HarmonyLib;
using Hazel.Udp;
using HoryTweaks.Core.Networking;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Configuration;
using InnerNet;
using UnityEngine;

namespace HoryTweaks.Networking;

/// <summary>
/// Applies safe, vanilla-server-compatible Hazel UDP and background-suspend tolerances
/// and tracks live connection health (ping, jitter, reliable resend spikes).
/// </summary>
[HarmonyPatch]
internal static class ConnectionResiliencePatch
{
    private const float SampleIntervalSeconds = 0.5f;

    internal static readonly ConnectionHealthTracker HealthTracker = new();

    private static nint _lastTunedConnectionPtr;
    private static float _nextSampleTime;

    /// <summary>
    /// Applies or restores connection tuning immediately on the active <see cref="AmongUsClient"/>.
    /// </summary>
    internal static void ApplyToActiveClient()
    {
        ApplyClientSettings();

        var client = AmongUsClient.Instance;
        if (client == null)
            return;

        if (client.connection != null)
        {
            var udp = client.connection.TryCast<UdpConnection>();
            if (udp != null)
            {
                ApplyUdpSettings(udp);
            }
        }
    }

    [HarmonyPatch(typeof(UnityUdpClientConnection), nameof(UnityUdpClientConnection.ConnectAsync))]
    [HarmonyPrefix]
    private static void UnityUdpClientConnection_ConnectAsync_Prefix(UnityUdpClientConnection __instance)
    {
        if (__instance == null || !BAUConfigs.NetworkOptimization.Value)
            return;

        ApplyUdpSettings(__instance);
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

        _lastTunedConnectionPtr = 0;
        HealthTracker.Reset();
        NetworkManager.ResetSession();
        ApplyClientSettings();

        if (__instance.connection != null)
        {
            var udp = __instance.connection.TryCast<UdpConnection>();
            if (udp != null)
            {
                ApplyUdpSettings(udp);
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
            if (connPtr != _lastTunedConnectionPtr)
            {
                _lastTunedConnectionPtr = connPtr;
                if (BAUConfigs.NetworkOptimization.Value)
                {
                    var udp = __instance.connection.TryCast<UdpConnection>();
                    if (udp != null)
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
        _lastTunedConnectionPtr = 0;

        if (reason != DisconnectReasons.ExitGame && reason != DisconnectReasons.Destroy)
        {
            BAUPlugin.Logger.Warning(
                $"Disconnected ({Enum.GetName(reason) ?? ((int)reason).ToString()}). Health: {HealthTracker.FormatDiagnosticSummary()} | Dispatch: {NetworkManager.FormatDiagnosticSummary()}",
                "NetworkHealth");
        }

        HealthTracker.Reset();
        NetworkManager.ResetSession();
    }

    private static void ApplyClientSettings()
    {
        if (BAUConfigs.NetworkOptimization.Value)
        {
            InnerNetClient.SecondsSuspendedBeforeDisconnect = Math.Max(
                InnerNetClient.SecondsSuspendedBeforeDisconnect,
                NetworkTuningPolicy.TargetSecondsSuspendedBeforeDisconnect);
        }
        else
        {
            InnerNetClient.SecondsSuspendedBeforeDisconnect = NetworkTuningPolicy.VanillaSecondsSuspendedBeforeDisconnect;
        }
    }

    private static void ApplyUdpSettings(UdpConnection udp)
    {
        if (!BAUConfigs.NetworkOptimization.Value)
            return;

        var unityUdp = udp.TryCast<UnityUdpClientConnection>();
        int currentReceiveBuffer = unityUdp != null ? unityUdp.ReceiveBufferSize : 0;
        int currentSuspend = InnerNetClient.SecondsSuspendedBeforeDisconnect;

        var tuned = NetworkTuningPolicy.ComputeTunedSettings(
            udp.DisconnectTimeoutMs,
            udp.ResendLimit,
            udp.ResendPingMultiplier,
            currentReceiveBuffer,
            currentSuspend);

        udp.KeepAliveInterval = tuned.KeepAliveIntervalMs;
        udp.DisconnectTimeoutMs = tuned.DisconnectTimeoutMs;
        udp.ResendTimeoutMs = tuned.ResendTimeoutMs;
        udp.ResendLimit = tuned.ResendLimit;
        udp.ResendPingMultiplier = tuned.ResendPingMultiplier;

        if (unityUdp != null)
        {
            unityUdp.ReceiveBufferSize = tuned.ReceiveBufferSize;
        }

        InnerNetClient.SecondsSuspendedBeforeDisconnect = tuned.SecondsSuspendedBeforeDisconnect;
        _lastTunedConnectionPtr = udp.Pointer;

        BAUPlugin.Logger.Log(
            $"Applied Hazel UDP tuning (keepAlive={tuned.KeepAliveIntervalMs}ms, timeout={tuned.DisconnectTimeoutMs}ms, resendLimit={tuned.ResendLimit}, resendMult={tuned.ResendPingMultiplier:F1}, rxBuf={tuned.ReceiveBufferSize / 1024}KiB, suspend={tuned.SecondsSuspendedBeforeDisconnect:F0}s)",
            "NetworkTuning");
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
