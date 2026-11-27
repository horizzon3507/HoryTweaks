

using AmongUs.Data;
using HoryTweaks.Core.Performance;
using HoryTweaks.Generated;

using HoryTweaks.Utilities;

using HarmonyLib;
using TMPro;
using UnityEngine;
using HoryTweaks.Features.Chat;
using HoryTweaks.Features.Hud;
using HoryTweaks.Features.Meeting;
using HoryTweaks.Features.MatchHistory;
using HoryTweaks.Features.MatchHistory.Video;
using HoryTweaks.Features.Minimap;
using HoryTweaks.Features.Moderation;
using HoryTweaks.Features.PlayerInfo;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.Persistence;
using HoryTweaks.Infrastructure.Persistence.Json;
using HoryTweaks.Game.Players;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Networking;

namespace HoryTweaks.Features.ClientOptions;

[HarmonyPatch]
internal static class OptionsMenuBehaviourPatch
{
    internal static TabGroup? BetterOptionsTab { get; private set; }
    private static TextMeshPro? _frameRateAdvisoryText;

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
    [HarmonyPostfix]
    private static void Start_Postfix(OptionsMenuBehaviour __instance)
    {
        // Create custom "Better Options" tab in settings menu
        BetterOptionsTab = CreateTabPage(__instance, TranslationStrings.BetterOption.LocalizedString);
        ModerationCenterMenu.Reset();
        MatchHistoryMenu.Reset();

        // Populate the tab with all BAU client options
        SetupAllClientOptions(__instance);

        // Reposition tabs to fit new Better Options tab
        UpdateTabPositions(__instance);
    }

    private static void SetupAllClientOptions(OptionsMenuBehaviour __instance)
    {
        if (__instance.DisableMouseMovement == null)
            return;

        // Clear previous client options to prevent duplicates
        ClientOptionItem.ClientOptions.Clear();

        // Page 1: what the player sees on screen
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_LessInfo, BAUConfigs.LessInfo, 1, __instance, MeetingHudPatch.UpdateHostIcon);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_LobbyInfo, BAUConfigs.LobbyPlayerInfo, 1, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_ShowFPS, BAUConfigs.ShowFPS, 1, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_BetterNotifications, BAUConfigs.BetterNotifications, 1, __instance, BetterNotificationManager.ClearNotifications);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_ChatDarkMode, BAUConfigs.ChatDarkMode, 1, __instance, ChatPatch.SetChatTheme);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_ForceOwnLanguage, BAUConfigs.ForceOwnLanguage, 1, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.Chat_ExtendedHistory, BAUConfigs.ChatExtendedHistory, 1, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_WindowAlerts, BAUConfigs.WindowAlerts, 1, __instance);

        // Page 2: in-game readability and map
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_BetterColorblindText, BAUConfigs.BetterColorblindText, 2, __instance, CosmeticsLayerPatch.UpdateAllColorblindText);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_ColorblindTextOnTop, BAUConfigs.ColorblindTextOnTop, 2, __instance, CosmeticsLayerPatch.UpdateAllColorblindText);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_MinimapIcons, BAUConfigs.MinimapIcons, 2, __instance, MiniMapBehaviourPatch.ForceCloseMiniMap);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_BetterMinimapColors, BAUConfigs.BetterMinimapColors, 2, __instance, MiniMapBehaviourPatch.ForceCloseMiniMap);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_VentColorGroups, BAUConfigs.VentColorGroups, 2, __instance, MiniMapBehaviourPatch.ForceCloseMiniMap);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_ScrollZoom, BAUConfigs.ScrollZoom, 2, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_LobbyTheme, BAUConfigs.DisableLobbyTheme, 2, __instance, ToggleLobbyTheme);

        // Page 3: performance, connection and files
        var unlockFpsItem = ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_UnlockFPS, BAUConfigs.UnlockFPS, 3, __instance, () => UpdateFrameRate(notifyPlayer: true));
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_NetworkOptimization, BAUConfigs.NetworkOptimization, 3, __instance, ConnectionResiliencePatch.ApplyToActiveClient);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_SendBetterRpc, BAUConfigs.SendBetterRpc, 3, __instance, SendBetterRpcAction);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_CompressSettingFiles, BAUConfigs.CompressSettingFiles, 3, __instance, ConvertAllSettingFiles);
        ClientOptionItem.CreateToggle(TranslationStrings.BetterOption_HideConsole, BAUConfigs.HideConsole, 3, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.Rejoin_AutoRejoin, BAUConfigs.AutoRejoin, 3, __instance);
        ClientOptionItem.CreateToggle(TranslationStrings.MatchHistory_Record, BAUConfigs.MatchHistory, 3, __instance, MatchRecorder.OnConfigToggled);
        ClientOptionItem.CreateToggle(TranslationStrings.MatchHistory_RecordVideo, BAUConfigs.MatchVideoReplay, 3, __instance, MatchVideoRecorder.OnConfigToggled);
        CreatePage3FrameRateAdvisory(unlockFpsItem, __instance);

        // Page 4: read-only list of hidden controls and gestures
        GestureHelpPage.Create(__instance);

        // Button options (no toggle)
        if (!BAUPlugin.ModInfo.Starlight)
        {
            ClientOptionItem.CreateButton(TranslationStrings.BetterOption_SaveData, -1, __instance, OpenSaveData, () =>
            {
                // Only allow opening save data in lobby/main menu, not during gameplay
                bool cannotOpen = GameState.IsInGame && !GameState.IsLobby;
                if (cannotOpen)
                {
                    BetterNotificationManager.Notify(TranslationStrings.BetterOption_SaveData_InGame.LocalizedString, 2.5f);
                }
                return !cannotOpen;
            });
        }

        ClientOptionItem.CreateButton(TranslationStrings.BetterOption_ToVanilla, -1, __instance, BAUPlugin.Instance.UnloadBAU, () =>
        {
            // Prevent switching to vanilla while in a game
            bool cannotSwitch = GameState.IsInGame;
            if (cannotSwitch)
            {
                BetterNotificationManager.Notify(TranslationStrings.BetterOption_ToVanilla_InGame.LocalizedString, 2.5f);
            }
            return !cannotSwitch;
        });

        ClientOptionItem.CreateButton(TranslationStrings.BetterOption_Moderation, -1, __instance, () => ModerationCenterMenu.Open(__instance));
        ClientOptionItem.CreateButton(TranslationStrings.BetterOption_MatchHistory, -1, __instance, () => MatchHistoryMenu.Open(__instance), MatchHistoryMenu.CanOpenWithNotice);
    }

    private static void SendBetterRpcAction()
    {
        // Resend handshake secret to all other players when option is toggled
        if (!GameState.IsInGame)
            return;

        foreach (var player in BAUPlugin.AllPlayerControls)
        {
            if (player.IsLocalPlayer()) continue;
            player.ExtendedData().HandshakeHandler.ResendSecretToPlayer();
        }
    }

    private static void ToggleLobbyTheme()
    {
        // Play lobby theme music if re-enabled while in lobby
        if (GameState.IsLobby && !BAUConfigs.DisableLobbyTheme.Value)
        {
            SoundManager.instance.CrossFadeSound("MapTheme", LobbyBehaviour.Instance.MapTheme, 0.5f, 1.5f);
        }
    }

    internal static FrameRateDecision LastFrameRateDecision { get; private set; } =
        new(FrameRatePolicy.DefaultLockedFps, 0, FrameRateWarningKind.None);

    /// <summary>
    /// Computes the live frame-rate decision from current VSync and display state (so /dump never reports stale VSync status).
    /// </summary>
    internal static FrameRateDecision GetCurrentFrameRateDecision()
    {
        var decision = FrameRatePolicy.Compute(BAUConfigs.UnlockFPS.Value, DetectMonitorRefreshRateHz(), IsGameVSyncEnabled());
        LastFrameRateDecision = decision;
        return decision;
    }

    internal static void UpdateFrameRate() => UpdateFrameRate(notifyPlayer: false);

    internal static void UpdateFrameRate(bool notifyPlayer)
    {
        var decision = GetCurrentFrameRateDecision();

        Application.targetFrameRate = decision.TargetFps;
        RefreshFrameRateAdvisory(decision);

        if (!BAUConfigs.UnlockFPS.Value)
        {
            BAUPlugin.Logger.Log($"Frame rate locked at {decision.TargetFps} FPS.", "Performance");
            return;
        }

        switch (decision.Warning)
        {
            case FrameRateWarningKind.VSyncConflict:
                BAUPlugin.Logger.Warning(
                    $"Unlock FPS target set to {decision.TargetFps} FPS (display {decision.DetectedRefreshHz}Hz), but Among Us VSync is enabled and will cap frame rate to the monitor refresh.",
                    "Performance");
                if (notifyPlayer && GameState.IsInGame)
                {
                    BetterNotificationManager.Notify(TranslationStrings.Performance_Warning_VSyncConflict.LocalizedString, 6f);
                }
                break;

            case FrameRateWarningKind.ClampedToSafeCap:
                BAUPlugin.Logger.Warning(
                    $"Display refresh ({decision.DetectedRefreshHz}Hz) exceeds safe ceiling; Unlock FPS capped at {decision.TargetFps} FPS to avoid CPU/network thread starvation.",
                    "Performance");
                if (notifyPlayer && GameState.IsInGame)
                {
                    BetterNotificationManager.Notify(TranslationStrings.Performance_Warning_FpsClamped.Format(decision.TargetFps), 5f);
                }
                break;

            case FrameRateWarningKind.RefreshRateUnknown:
                BAUPlugin.Logger.Warning(
                    $"Could not detect monitor refresh rate; Unlock FPS set to safe fallback {decision.TargetFps} FPS.",
                    "Performance");
                break;

            default:
                BAUPlugin.Logger.Log(
                    $"Unlock FPS active: target {decision.TargetFps} FPS (display {decision.DetectedRefreshHz}Hz).",
                    "Performance");
                break;
        }
    }

    private static void CreatePage3FrameRateAdvisory(ClientOptionItem templateItem, OptionsMenuBehaviour menu)
    {
        var page3 = ClientOptionItem.GetOrCreatePage(3, menu, doNotCreate: true);
        if (page3 == null || templateItem.ToggleButton == null || templateItem.ToggleButton.Text == null)
            return;

        var label = UnityEngine.Object.Instantiate(templateItem.ToggleButton.Text, page3.transform);
        label.name = "UnlockFpsAdvisory_TMP";
        label.transform.localPosition = new Vector3(0f, -0.45f, -6f);
        label.fontSize = 1.55f;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.72f, 0.3f, 1f);
        label.rectTransform.sizeDelta = new Vector2(5.2f, 0.7f);
        _frameRateAdvisoryText = label;

        RefreshFrameRateAdvisory(GetCurrentFrameRateDecision());
    }

    private static void RefreshFrameRateAdvisory(FrameRateDecision decision)
    {
        if (_frameRateAdvisoryText == null)
            return;

        string message = decision.Warning switch
        {
            FrameRateWarningKind.VSyncConflict => $"{TranslationStrings.WarningIcon.LocalizedString} {TranslationStrings.Performance_Warning_VSyncConflict.LocalizedString}",
            FrameRateWarningKind.ClampedToSafeCap => $"{TranslationStrings.WarningIcon.LocalizedString} {TranslationStrings.Performance_Warning_FpsClamped.Format(decision.TargetFps)}",
            _ => string.Empty
        };

        _frameRateAdvisoryText.SetText(message);
        bool visible = message.Length > 0;
        if (_frameRateAdvisoryText.gameObject.activeSelf != visible)
        {
            _frameRateAdvisoryText.gameObject.SetActive(visible);
        }
    }

    private static double DetectMonitorRefreshRateHz()
    {
        try
        {
            var current = Screen.currentResolution;
            double hz = current.m_RefreshRate.value;
            if (hz > 0.0)
                return hz;

            var resolutions = Screen.resolutions;
            if (resolutions != null)
            {
                double best = 0.0;
                for (int i = 0; i < resolutions.Length; i++)
                {
                    double candidate = resolutions[i].m_RefreshRate.value;
                    if (candidate > best && candidate <= 1000.0)
                    {
                        best = candidate;
                    }
                }

                return best;
            }
        }
        catch
        {
        }

        return 0.0;
    }

    private static bool IsGameVSyncEnabled()
    {
        try
        {
            return DataManager.Settings != null
                && DataManager.Settings.Video != null
                && DataManager.Settings.Video.VSync;
        }
        catch
        {
            return false;
        }
    }

    private static void OpenSaveData()
    {
        // Open BAU save data folder in file explorer
        if (!File.Exists(BetterDataManager.Files.dataFilePath))
            return;

        if (!HoryTweaks.Infrastructure.Platform.ShellOpener.TryOpen(BetterDataManager.Files.dataFilePath, isDirectory: false))
        {
            BetterNotificationManager.Notify(TranslationStrings.Shell_OpenFailed.LocalizedString, 2.5f);
        }
    }

    private static void ConvertAllSettingFiles()
    {
        for (int i = 0; i < 10; i++)
        {
            if (i == BAUConfigs.SettingsPreset.Value)
            {
                BetterDataManager.Files.BetterGameSettingsFile.Save();
                continue;
            }

            string path = BetterDataManager.GetSettingsFilePathFromPreset(i);
            if (File.Exists(path))
            {
                var settings = new BetterGameSettingsFile
                {
                    OverrideFilePath = path
                };
                settings.Init();
            }
        }
    }

    private static TabGroup CreateTabPage(OptionsMenuBehaviour __instance, string name)
    {
        // Clone last tab as template for new Better Options tab
        var tabPrefab = __instance.Tabs[^1];
        var tab = UnityEngine.Object.Instantiate(tabPrefab, tabPrefab.transform.parent);

        tab.name = $"{name}Button";
        tab.DestroyTextTranslators();
        tab.GetComponentInChildren<TextMeshPro>(true)?.SetText(name);
        tab.gameObject.SetActive(true);

        // Create content container for the new tab
        var content = new GameObject($"{name}Tab");
        content.SetActive(false);
        content.transform.SetParent(tab.Content.transform.parent);
        content.transform.localScale = Vector3.one;
        tab.Content = content;

        // Add new tab to the tabs array
        var tabs = new List<TabGroup>(__instance.Tabs) { tab };
        __instance.Tabs = tabs.ToArray();

        // Set up click handler for the tab button
        var index = __instance.Tabs.Length - 1;
        var button = tab.GetComponent<PassiveButton>();
        button.OnClick = new();
        button.OnClick.AddListener(() =>
        {
            tab.Rollover.SetEnabledColors();
            __instance.OpenTabGroup(index);
        });

        return tab;
    }

    private static void UpdateTabPositions(OptionsMenuBehaviour __instance)
    {
        // Position tabs based on game state (in-game vs main menu)
        Vector3 basePos = new(0f, !GameState.InGame ? 0 : 2.5f, -1f);
        const float buttonSpacing = 0.6f;
        const float buttonWidth = 1.0f;

        // Count only active tabs
        int activeCount = 0;
        foreach (var tabButton in __instance.Tabs)
        {
            if (tabButton.gameObject.activeInHierarchy) activeCount++;
        }

        if (activeCount == 0)
            return;

        // Calculate total width needed for all active tabs
        float totalWidth = (activeCount - 1) * buttonSpacing + activeCount * buttonWidth;
        float startX = basePos.x - (totalWidth / 2f) + (buttonWidth / 2f);

        // Position each active tab evenly spaced
        int activeIndex = 0;
        foreach (var tabButton in __instance.Tabs)
        {
            if (!tabButton.gameObject.activeInHierarchy) continue;

            float xPos = startX + activeIndex * (buttonWidth + buttonSpacing);
            tabButton.transform.localPosition = new Vector3(xPos, basePos.y, basePos.z);
            activeIndex++;
        }
    }
}