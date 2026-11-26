using HoryTweaks.Core.Networking;
using HoryTweaks.Core.Performance;
using HoryTweaks.Features.Chat;
using HoryTweaks.Game;
using HoryTweaks.Generated;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Networking;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Utilities;
using System.Text;
using TMPro;
using UnityEngine;
using static HoryTweaks.Features.Lobby.LobbyPatch;

namespace HoryTweaks.Features.Hud;

/// <summary>
/// Provides throttled, low-allocation ping, FPS, and connection/frame-pacing warning display.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class BetterPingTracker : MonoBehaviour
{
    private const float RefreshIntervalSeconds = 0.25f;

    private static readonly Color[] StatusGradient =
    [
        Color.green,
        Color.yellow,
        new Color(1f, 0.5f, 0f),
        Color.red
    ];

    private static readonly Vector3 ChatVisibleOffset = new(4.6f, 0.1f, -5f);
    private static readonly Vector3 ChatHiddenOffset = new(4f, 0.1f, -5f);

    /// <summary>
    /// Shared frame-pacing tracker used by both the HUD and /dump diagnostics.
    /// </summary>
    internal static readonly FramePacingTracker FrameTracker = new();

    /// <summary>
    /// Gets the singleton instance of the BetterPingTracker.
    /// </summary>
    internal static BetterPingTracker? Instance { get; private set; }

    private AspectPosition? _aspectPosition;
    private TextMeshPro? _text;
    private readonly StringBuilder _sb = new(256);
    private string _lastRenderedText = string.Empty;
    private bool _hasLastChatVisible;
    private bool _lastChatVisible;
    private float _nextRefreshTime;

    /// <summary>
    /// Initializes the BetterPingTracker with the required UI components.
    /// </summary>
    internal void SetUp(TextMeshPro pingText, AspectPosition pingAspectPosition)
    {
        if (Instance != null)
            return;

        if (pingText == null || pingAspectPosition == null)
        {
            BAUPlugin.Logger.Error("BetterPingTracker.SetUp() called with null parameters!");
            return;
        }

        Instance = this;
        _text = pingText;
        _aspectPosition = pingAspectPosition;
        _aspectPosition.Alignment = AspectPosition.EdgeAlignments.RightTop;
        _text.outlineWidth = 0.3f;
        _hasLastChatVisible = false;
        _lastRenderedText = string.Empty;
        _nextRefreshTime = 0f;
    }

    private void Update()
    {
        if (_aspectPosition == null || _text == null)
            return;

        // Track frame pacing every frame with zero allocations so severe hitches are never missed.
        var frameWarning = FrameTracker.RecordFrame(Time.unscaledDeltaTime, Time.unscaledTimeAsDouble);
        if (frameWarning == FrameWarningKind.SevereHitch)
        {
            BAUPlugin.Logger.Warning(
                $"Main-thread frame hitch detected ({FrameTracker.LastHitchMs}ms, avg {FrameTracker.SmoothedFps:F0} FPS). Heavy stalls can cause ping spikes or disconnects.",
                "Performance");
        }
        else if (frameWarning == FrameWarningKind.SustainedLowFps)
        {
            BAUPlugin.Logger.Warning(
                $"Sustained low FPS detected ({FrameTracker.SmoothedFps:F0} FPS, peak frame {FrameTracker.PeakFrameMs:F0}ms).",
                "Performance");
        }

        bool chatVisible = ChatPatch.IsChatVisible;
        if (!_hasLastChatVisible || chatVisible != _lastChatVisible)
        {
            _aspectPosition.DistanceFromEdge = chatVisible ? ChatVisibleOffset : ChatHiddenOffset;
            _lastChatVisible = chatVisible;
            _hasLastChatVisible = true;
        }

        float now = Time.unscaledTime;
        if (now < _nextRefreshTime)
            return;

        _nextRefreshTime = now + RefreshIntervalSeconds;

        BuildTrackerText();
        string rendered = _sb.ToString();
        if (!string.Equals(rendered, _lastRenderedText, StringComparison.Ordinal))
        {
            _text.SetText(rendered);
            _lastRenderedText = rendered;
        }
    }

    private void BuildTrackerText()
    {
        _sb.Clear();

        bool lessInfo = BAUConfigs.LessInfo.Value;
        var client = AmongUsClient.Instance;

        if (!lessInfo && client != null && !GameState.IsFreePlay)
        {
            int ping = client.Ping;
            string pingColor = Utils.Color32ToHex(Utils.LerpColor(StatusGradient, (25f, 250f), ping));
            _sb.Append(TranslationStrings.Ping.LocalizedString.ToUpperInvariant())
                .Append(": <b><")
                .Append(pingColor)
                .Append('>')
                .Append(ping)
                .Append("ms</color></b>\n");

            AppendConnectionWarning();
        }

        if (GameState.IsLobby && GameState.IsHost && GameState.IsVanillaServer && !GameState.IsLocalGame)
        {
            string timeColor = Utils.Color32ToHex(Utils.LerpColor(StatusGradient, (0f, 300f), lobbyTimer, reverse: true));
            _sb.Append(TranslationStrings.Timer.LocalizedString.ToUpperInvariant())
                .Append(": <b><")
                .Append(timeColor)
                .Append('>')
                .Append(lobbyTimerDisplay)
                .Append("</color></b>\n");
        }

        if (!lessInfo)
        {
            _sb.Append("<color=#00dbdb><size=75%>")
                .Append(BAUPlugin.ModInfo.PLUGIN_NAME)
                .Append(' ')
                .Append(BAUPlugin.ModInfo.VERSION_STRING)
                .Append("</size></color>\n")
                .Append("<size=52%><color=#8A8A8A>")
                .Append(BAUPlugin.ModInfo.GITHUB)
                .Append("</color></size>\n");
        }

        if (BAUConfigs.ShowFPS.Value)
        {
            int fps = Mathf.RoundToInt(FrameTracker.SmoothedFps);
            _sb.Append("<color=#ffffbe><size=75%>FPS: <b>")
                .Append(fps)
                .Append("</b></size></color>\n");
        }

        if (!lessInfo)
        {
            AppendFrameWarning();
        }

        if (!lessInfo && GameState.IsInGamePlay && !GameState.IsFreePlay && client != null && !GameState.IsMeeting)
        {
            var hostInfo = client.GetHost();
            if (hostInfo != null && hostInfo.Character != null)
            {
                _sb.Append("<size=75%>")
                    .Append(TranslationStrings.Host.LocalizedString)
                    .Append(": ")
                    .Append(hostInfo.Character.GetPlayerNameAndColor())
                    .Append("</size>\n");
            }
        }
    }

    private void AppendConnectionWarning()
    {
        var health = ConnectionResiliencePatch.HealthTracker;
        switch (health.ActiveWarning)
        {
            case ConnectionWarningKind.PacketLoss:
                _sb.Append("<color=#ff5252><size=70%>")
                    .Append(TranslationStrings.WarningIcon.LocalizedString)
                    .Append(' ')
                    .Append(TranslationStrings.Network_Warning_PacketLoss.Format(health.RecentResends))
                    .Append("</size></color>\n");
                break;

            case ConnectionWarningKind.HighJitter:
                _sb.Append("<color=#ffae42><size=70%>")
                    .Append(TranslationStrings.WarningIcon.LocalizedString)
                    .Append(' ')
                    .Append(TranslationStrings.Network_Warning_HighJitter.Format(Mathf.RoundToInt(health.JitterMs)))
                    .Append("</size></color>\n");
                break;

            case ConnectionWarningKind.HighPing:
                _sb.Append("<color=#ffae42><size=70%>")
                    .Append(TranslationStrings.WarningIcon.LocalizedString)
                    .Append(' ')
                    .Append(TranslationStrings.Network_Warning_HighPing.Format(health.CurrentPingMs))
                    .Append("</size></color>\n");
                break;
        }
    }

    private void AppendFrameWarning()
    {
        switch (FrameTracker.ActiveWarning)
        {
            case FrameWarningKind.SevereHitch:
                _sb.Append("<color=#ffae42><size=70%>")
                    .Append(TranslationStrings.WarningIcon.LocalizedString)
                    .Append(' ')
                    .Append(TranslationStrings.Performance_Warning_FrameHitch.Format(FrameTracker.LastHitchMs))
                    .Append("</size></color>\n");
                break;

            case FrameWarningKind.SustainedLowFps:
                _sb.Append("<color=#ffae42><size=70%>")
                    .Append(TranslationStrings.WarningIcon.LocalizedString)
                    .Append(' ')
                    .Append(TranslationStrings.Performance_Warning_LowFps.Format(Mathf.RoundToInt(FrameTracker.SmoothedFps)))
                    .Append("</size></color>\n");
                break;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
