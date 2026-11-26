using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using HoryTweaks.Core.Rejoin;
using HoryTweaks.Generated;
using HoryTweaks.Infrastructure.Configuration;
using InnerNet;
using UnityEngine;

namespace HoryTweaks.Features.Rejoin;

/// <summary>
/// Remembers the last online lobby the local player joined and, after an
/// involuntary disconnect, retries joining it for a short window with a
/// visible countdown on the disconnect popup.
/// </summary>
internal static class AutoRejoinManager
{
    private const float TickSeconds = 1f;

    private static int _lastGameId;
    private static IRegionInfo? _lastRegion;
    private static bool _hasLastLobby;
    private static bool _rejoinActive;
    private static bool _attemptInFlight;
    private static float _attemptStartedAt;

    // Called when the local client finishes joining a game.
    internal static void NotifyGameJoined()
    {
        _rejoinActive = false;
        _attemptInFlight = false;

        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || client.NetworkMode != NetworkModes.OnlineGame)
        {
            _hasLastLobby = false;
            return;
        }

        _lastGameId = client.GameId;
        var serverManager = ServerManager.Instance;
        _lastRegion = serverManager != null ? serverManager.CurrentRegion : null;
        _hasLastLobby = true;
    }

    // Called from InnerNetClient.DisconnectInternal whenever the local client disconnects.
    internal static void NotifyDisconnected(DisconnectReasons reason)
    {
        if (_rejoinActive)
        {
            if (RejoinPolicy.ShouldCancelRejoin((int)reason))
            {
                _rejoinActive = false;
                _attemptInFlight = false;
            }
            return;
        }

        if (!BAUConfigs.AutoRejoin.Value || !_hasLastLobby)
            return;

        if (!RejoinPolicy.ShouldAttemptRejoin((int)reason))
            return;

        var client = AmongUsClient.Instance;
        if (client == null)
            return;

        BAUPlugin.Logger.Log($"Auto-rejoin: retrying lobby {GameCode.IntToGameName(_lastGameId)} after {Enum.GetName(reason)}", "AutoRejoin");
        client.StartCoroutine(CoAutoRejoin());
    }

    // Called when the user dismisses the disconnect popup.
    internal static void NotifyPopupClosed()
    {
        _rejoinActive = false;
        _attemptInFlight = false;
    }

    private static IEnumerator CoAutoRejoin()
    {
        _rejoinActive = true;
        var client = AmongUsClient.Instance;
        if (client == null)
        {
            _rejoinActive = false;
            yield break;
        }

        string code = GameCode.IntToGameName(_lastGameId);
        float deadline = Time.unscaledTime + RejoinPolicy.RejoinWindowSeconds;
        // Brief initial delay lets the server tear down the dropped session cleanly before retry #1.
        float nextAttemptAt = Time.unscaledTime + RejoinPolicy.InitialRetryDelaySeconds;
        int attemptNumber = 0;

        while (_rejoinActive)
        {
            float now = Time.unscaledTime;
            float remaining = deadline - now;
            if (remaining <= 0f)
                break;

            var popup = DisconnectPopup.Instance;
            if (popup == null)
                break;

            popup.SetText($"{TranslationStrings.Rejoin_Reconnecting.Format(code, Mathf.CeilToInt(remaining))}\n{TranslationStrings.Rejoin_CancelHint.LocalizedString}");

            if (now >= nextAttemptAt && !_attemptInFlight)
            {
                attemptNumber++;
                nextAttemptAt = now + RejoinPolicy.GetRetryDelaySeconds(attemptNumber);
                SwitchBackToLastRegion();

                var activeClient = AmongUsClient.Instance;
                if (activeClient == null)
                    break;

                activeClient.StartCoroutine(CoAttemptJoin(_lastGameId));
            }

            // Never let a hung attempt block the rest of the window.
            if (_attemptInFlight && now - _attemptStartedAt > RejoinPolicy.AttemptTimeoutSeconds)
            {
                _attemptInFlight = false;
            }

            yield return new WaitForSecondsRealtime(TickSeconds);
        }

        if (_rejoinActive && Time.unscaledTime >= deadline)
        {
            BAUPlugin.Logger.Log($"Auto-rejoin: gave up rejoining {code}", "AutoRejoin");
            var popup = DisconnectPopup.Instance;
            if (popup != null)
            {
                popup.SetText(TranslationStrings.Rejoin_Failed.Format(code));
            }
        }

        _rejoinActive = false;
        _attemptInFlight = false;
    }

    private static IEnumerator CoAttemptJoin(int gameId)
    {
        _attemptInFlight = true;
        _attemptStartedAt = Time.unscaledTime;

        var client = AmongUsClient.Instance;
        if (client != null)
        {
            yield return client.StartCoroutine(client.CoJoinOnlineGameFromCode(gameId, false));
        }

        _attemptInFlight = false;
    }

    private static void SwitchBackToLastRegion()
    {
        var serverManager = ServerManager.Instance;
        if (serverManager == null || _lastRegion == null)
            return;

        if (serverManager.CurrentRegion == null || serverManager.CurrentRegion.Name != _lastRegion.Name)
        {
            serverManager.SetRegion(_lastRegion);
        }
    }
}
