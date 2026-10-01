using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using InnerNet;
using UnityEngine;

namespace BetterAmongUs.Managers;

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

        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || client.NetworkMode != NetworkModes.OnlineGame)
        {
            _hasLastLobby = false;
            return;
        }

        _lastGameId = client.GameId;
        _lastRegion = ServerManager.Instance?.CurrentRegion;
        _hasLastLobby = true;
    }

    // Called from InnerNetClient.DisconnectInternal whenever the local client disconnects.
    internal static void NotifyDisconnected(DisconnectReasons reason)
    {
        if (_rejoinActive)
        {
            // A result arrived while retrying: definite removals stop the loop,
            // transient failures (game not found, lobby full, ...) keep it going.
            if (RejoinPolicy.ShouldCancelRejoin((int)reason))
            {
                _rejoinActive = false;
            }
            return;
        }

        if (!BAUConfigs.AutoRejoin.Value || !_hasLastLobby)
            return;

        if (!RejoinPolicy.ShouldAttemptRejoin((int)reason))
            return;

        BAUPlugin.Logger.Log($"Auto-rejoin: retrying lobby {GameCode.IntToGameName(_lastGameId)} after {Enum.GetName(reason)}", "AutoRejoin");
        AmongUsClient.Instance.StartCoroutine(CoAutoRejoin());
    }

    // Called when the user dismisses the disconnect popup.
    internal static void NotifyPopupClosed()
    {
        _rejoinActive = false;
    }

    private static IEnumerator CoAutoRejoin()
    {
        _rejoinActive = true;
        var client = AmongUsClient.Instance;
        string code = GameCode.IntToGameName(_lastGameId);
        float remaining = RejoinPolicy.RejoinWindowSeconds;
        float nextAttemptIn = 0f;

        while (_rejoinActive && remaining > 0f)
        {
            var popup = DisconnectPopup.Instance;
            if (popup == null)
            {
                break;
            }

            popup.SetText($"{TranslationStrings.Rejoin_Reconnecting.Format(code, Mathf.CeilToInt(remaining))}\n{TranslationStrings.Rejoin_CancelHint.LocalizedString}");

            nextAttemptIn -= TickSeconds;
            if (nextAttemptIn <= 0f && !_attemptInFlight)
            {
                nextAttemptIn = RejoinPolicy.AttemptIntervalSeconds;
                SwitchBackToLastRegion();
                client.StartCoroutine(CoAttemptJoin(_lastGameId));
            }

            // Never let a hung attempt block the rest of the window.
            if (_attemptInFlight && Time.time - _attemptStartedAt > RejoinPolicy.AttemptTimeoutSeconds)
            {
                _attemptInFlight = false;
            }

            yield return new WaitForSeconds(TickSeconds);
            remaining -= TickSeconds;
        }

        if (_rejoinActive && remaining <= 0f)
        {
            BAUPlugin.Logger.Log($"Auto-rejoin: gave up rejoining {code}", "AutoRejoin");
            DisconnectPopup.Instance?.SetText(TranslationStrings.Rejoin_Failed.Format(code));
        }

        _rejoinActive = false;
        _attemptInFlight = false;
    }

    private static IEnumerator CoAttemptJoin(int gameId)
    {
        _attemptInFlight = true;
        _attemptStartedAt = Time.time;

        // Reuse the game's normal join-by-code flow instead of hand-crafting joins.
        yield return AmongUsClient.Instance.StartCoroutine(
            AmongUsClient.Instance.CoJoinOnlineGameFromCode(gameId, false));

        _attemptInFlight = false;
    }

    private static void SwitchBackToLastRegion()
    {
        var serverManager = ServerManager.Instance;
        if (serverManager == null || _lastRegion == null)
        {
            return;
        }

        // Game codes are region-scoped: move back before asking the matchmaker.
        if (serverManager.CurrentRegion == null || serverManager.CurrentRegion.Name != _lastRegion.Name)
        {
            serverManager.SetRegion(_lastRegion);
        }
    }
}
