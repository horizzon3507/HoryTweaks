

using HoryTweaks.Modules.OptionItems;
using HoryTweaks.Modules.Support;

using HoryTweaks.Utilities;
using HarmonyLib;
using UnityEngine;
using HoryTweaks.Features.HostTransfer;
using HoryTweaks.Features.Notifications;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Localization;

namespace HoryTweaks.Features.Lobby;

[HarmonyPatch]
internal static class LobbyPatch
{
    private static Transform? GameStartObj;
    private static bool _lobbyThemeStopped;
    private static int _lastDisplayedLobbySeconds = -1;
    private static string? _lastStartButtonText;

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    [HarmonyPostfix]
    private static void LobbyBehaviour_Start_Postfix()
    {
        // Reset player selection options when lobby starts
        OptionPlayerItem.ResetAllValues();
        WindowAlertManager.ResetLobby();
        _lobbyThemeStopped = false;
    }

    // Disable annoying lobby music if setting is enabled
    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
    [HarmonyPostfix]
    private static void LobbyBehaviour_Update_Postfix()
    {
        if (BAUConfigs.DisableLobbyTheme.Value)
        {
            if (!_lobbyThemeStopped && LobbyBehaviour.Instance != null)
            {
                SoundManager.instance.StopSound(LobbyBehaviour.Instance.MapTheme);
                _lobbyThemeStopped = true;
            }
        }
        else
        {
            _lobbyThemeStopped = false;
        }

        // Adjust GameStartManager position for better UI layout
        if (GameStartObj == null)
        {
            if (HudManager.InstanceExists)
            {
                GameStartObj = HudManager.Instance.transform.Find("GameStartManager");
                if (GameStartObj != null)
                {
                    GameStartObj.SetLocalY(-2.8f);
                }
            }
        }
        else if (Mathf.Abs(GameStartObj.localPosition.y - (-2.8f)) > 0.001f)
        {
            GameStartObj.SetLocalY(-2.8f);
        }

        if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
        {
            WindowAlertManager.CheckLobbyFull(BAUPlugin.AllPlayerControls.Count, GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers);
        }

        AfkHostTransfer.Tick();
    }

    [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.RpcExtendLobbyTimer))]
    [HarmonyPostfix]
    private static void LobbyBehaviour_RpcExtendLobbyTimer_Postfix()
    {
        lobbyTimer += 30f; // Add 30 seconds to lobby timer
    }

    // Apply UI colors to settings pane buttons
    [HarmonyPatch(typeof(LobbyViewSettingsPane), nameof(LobbyViewSettingsPane.Awake))]
    [HarmonyPostfix]
    private static void LobbyViewSettingsPane_Awake_Postfix(LobbyViewSettingsPane __instance)
    {
        __instance.backButton.gameObject.SetUIColors("Icon");
        __instance.taskTabButton.gameObject.SetUIColors("Icon");
        __instance.rolesTabButton.gameObject.SetUIColors("Icon");
    }

    internal static float lobbyTimer = 600f; // 10 minute default
    internal static string lobbyTimerDisplay = "";

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    [HarmonyPostfix]
    private static void GameStartManager_Start_Postfix(GameStartManager __instance)
    {
        lobbyTimer = 600f; // Reset timer
        _lastDisplayedLobbySeconds = -1;
        _lastStartButtonText = null;

        var extendedPassiveButton = IMonoExtension.AddExtension<ExtendedPassiveButton>(__instance.StartButton);
        extendedPassiveButton.OnHoldOrShiftClick += () =>
        {
            if (__instance.startState == GameStartManager.StartingStates.NotStarting)
            {
                __instance.startState = GameStartManager.StartingStates.Countdown;
                __instance.FinallyBegin();
            }
        };

        // Apply UI colors to buttons
        __instance.StartButton.gameObject.SetUIColors("Icon");
        __instance.EditButton.gameObject.SetUIColors("Icon");
        __instance.ClientViewButton.gameObject.SetUIColors("Icon");
        __instance.HostViewButton.gameObject.SetUIColors("Icon");

        // Move start buttons to host panel if ping tracker not disabled
        if (!BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_BetterPingTracker))
        {
            __instance.StartButton.transform.SetParent(__instance.HostInfoPanel?.transform);
            __instance.StartButtonClient.transform.SetParent(__instance.HostInfoPanel?.transform);
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPrefix]
    private static void GameStartManager_Update_Prefix(GameStartManager __instance)
    {
        // Update lobby timer countdown and only reformat string when whole seconds change
        lobbyTimer = Mathf.Max(0f, lobbyTimer - Time.deltaTime);
        int totalSeconds = (int)lobbyTimer;
        if (totalSeconds != _lastDisplayedLobbySeconds)
        {
            _lastDisplayedLobbySeconds = totalSeconds;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            lobbyTimerDisplay = $"{minutes:00}:{seconds:00}";
        }

        __instance.MinPlayers = 1; // Allow starting with just 1 player
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPostfix]
    private static void GameStartManager_Update_Postfix(GameStartManager __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_CancelStartingGame))
            return;

        // Hide start button for non-hosts
        if (!GameState.IsHost)
        {
            if (__instance.StartButton.gameObject.activeSelf)
            {
                __instance.StartButton.gameObject.SetActive(false);
            }
            return;
        }

        // Custom start button behavior for hosts
        if (__instance.GameStartTextParent.activeSelf)
        {
            __instance.GameStartTextParent.SetActive(false);
        }

        if (!__instance.StartButton.gameObject.activeSelf)
        {
            __instance.StartButton.gameObject.SetActive(true);
        }

        string label = __instance.startState == GameStartManager.StartingStates.Countdown
            ? $"{Translator.GetString(StringNames.Cancel)}: {(int)__instance.countDownTimer + 1}"
            : Translator.GetString(StringNames.StartLabel);

        if (!string.Equals(_lastStartButtonText, label, StringComparison.Ordinal))
        {
            __instance.StartButton.buttonText.text = label;
            _lastStartButtonText = label;
        }
    }

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
    [HarmonyPrefix]
    private static bool GameStartManager_BeginGame_Prefix(GameStartManager __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_CancelStartingGame)) return true;

        // If countdown is active, clicking cancels the start
        if (__instance.startState == GameStartManager.StartingStates.Countdown)
        {
            SoundManager.instance.StopSound(__instance.gameStartSound);
            __instance.ResetStartState();
            return false;
        }

        return true;
    }

    // Log game start info
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.FinallyBegin))]
    [HarmonyPrefix]
    private static void GameStartManager_FinallyBegin_Prefix(/*GameStartManager __instance*/)
    {
        WindowAlertManager.NotifyGameStarted();
        BAUPlugin.Logger.LogHeader($"Game Has Started - {Enum.GetName(typeof(MapNames), GameState.GetActiveMapId)}/{GameState.GetActiveMapId}", "GamePlayManager");
    }
}