using BetterAmongUs.Data;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.OptionItems;
using BetterAmongUs.Modules.OptionItems.NoneOption;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Utilities;
using HarmonyLib;
using UnityEngine;

namespace BetterAmongUs.Patches.Gameplay.UI.Settings;

// Custom game settings definitions
internal sealed class BetterGameSettings
{
    internal static OptionFloatItem? KickCooldown;
    internal static OptionCheckboxItem? InvalidFriendCode;
    internal static OptionCheckboxItem? UseBanPlayerList;
    internal static OptionCheckboxItem? UseBanNameList;
    internal static OptionCheckboxItem? UseBanChatList;
    internal static OptionCheckboxItem? UseBanChatListOnlyLobby;
    internal static OptionCheckboxItem? UseBanChatListBan;
    internal static OptionCheckboxItem? KickLevel;
    internal static OptionIntItem? KickLevelBelow;
    internal static OptionIntItem? KickLevelBelowMinimumPlayers;
    internal static OptionCheckboxItem? RpcRateLimiting;
    internal static OptionIntItem? RpcRateLimit;

    internal static OptionCheckboxItem? CancelInvalidSabotage;
}

// Temporary settings for Hide & Seek impostor selection
/*
internal sealed class BetterGameSettingsTemp
{
    internal static OptionPlayerItem? HideAndSeekImp2;
    internal static OptionPlayerItem? HideAndSeekImp3;
    internal static OptionPlayerItem? HideAndSeekImp4;
    internal static OptionPlayerItem? HideAndSeekImp5;
}
*/

[HarmonyPatch]
internal static class GameSettingsPatch
{
    internal static OptionTab? BetterSettingsTab;

    // Creates all custom game settings and organizes them in tabs
    internal static void SetupSettings(bool IsPreload = false)
    {
        // Note: Use 2200 next ID

        // Create main settings tab
        BetterSettingsTab = OptionTab.Create(3, TranslationStrings.BetterSetting, TranslationStrings.BetterSetting_Description, Colors.Theme);
        OptionSearchField.Create(BetterSettingsTab);

        OptionHeaderItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_MainHeader_System);
        OptionPresetItem.Create().CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_Presets_Description);
        SetupPresetActions(BetterSettingsTab);

        // Host tools
        {
            OptionHeaderItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_MainHeader_HostTools);

            // Host moderation settings
            if (IsPreload || GameState.IsHost)
            {
                OptionTitleItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_TextHeader_HostOnly);
                BetterGameSettings.KickCooldown = OptionFloatItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_KickCooldown, (0f, 3f, 0.1f), 2f, ("", "s"));
                BetterGameSettings.KickCooldown.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_KickCooldown_Description);
                BetterGameSettings.InvalidFriendCode = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_InvalidFriendCode, true);
                BetterGameSettings.InvalidFriendCode.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_InvalidFriendCode_Description);
                BetterGameSettings.CancelInvalidSabotage = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_CancelInvalidSabotage, true);
                BetterGameSettings.UseBanPlayerList = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_UseBanPlayerList, true);
                BetterGameSettings.UseBanPlayerList.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_UseBanPlayerList_Description);
                BetterGameSettings.UseBanNameList = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_UseBanNameList, true);
                BetterGameSettings.UseBanNameList.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_UseBanNameList_Description);
                BetterGameSettings.UseBanChatList = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_UseBanChatList, true);
                BetterGameSettings.UseBanChatList.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_UseBanChatList_Description);
                BetterGameSettings.UseBanChatListOnlyLobby = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_UseBanChatListOnlyLobby, true, BetterGameSettings.UseBanChatList);
                BetterGameSettings.UseBanChatListBan = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_UseBanChatListBan, false, BetterGameSettings.UseBanChatList);
            }

            BetterGameSettings.KickLevel = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_KickLevel, false);
            BetterGameSettings.KickLevel.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_KickLevel_Description);
            BetterGameSettings.KickLevelBelow = OptionIntItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_KickLevelBelow, (0, 10000, 1), 0, ("Lv ", ""), BetterGameSettings.KickLevel);
            BetterGameSettings.KickLevelBelowMinimumPlayers = OptionIntItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_KickLevelBelowMinimumPlayers, (1, 15, 1), 9, parent: BetterGameSettings.KickLevelBelow);
            BetterGameSettings.KickLevelBelowMinimumPlayers.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_KickLevelBelowMinimumPlayers_Description);
            BetterGameSettings.RpcRateLimiting = OptionCheckboxItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_RpcRateLimiting, true);
            BetterGameSettings.RpcRateLimiting.CreateDescriptionButton(TranslationStrings.BetterSetting_Setting_RpcRateLimiting_Description);
            BetterGameSettings.RpcRateLimit = OptionIntItem.Create(BetterSettingsTab, TranslationStrings.BetterSetting_Setting_RateLimit, (25, 1000, 1), 50, ("", " PS"), BetterGameSettings.RpcRateLimiting);
        }

        BetterSettingsTab.UpdateVisuals();
    }

    // Restore-defaults and clipboard sharing rows for the selected preset
    private static void SetupPresetActions(OptionTab tab)
    {
        OptionButtonItem.Create(tab, TranslationStrings.BetterSetting_Action_RestoreDefaults, TranslationStrings.BetterSetting_Action_Reset, () =>
        {
            int count = OptionItem.RestoreDefaults();
            OptionButtonItem.Notify(TranslationStrings.BetterSetting_Action_RestoreDefaults_Done.Format(count.ToString()));
        }, TranslationStrings.BetterSetting_Action_Confirm);

        OptionButtonItem.Create(tab, TranslationStrings.BetterSetting_Action_ExportPreset, TranslationStrings.BetterSetting_Action_Copy, () =>
        {
            ClipboardHelper.PutClipboardString(OptionPresetItem.Export());
            OptionButtonItem.Notify(TranslationStrings.BetterSetting_Action_ExportPreset_Done.LocalizedString);
        });

        Dictionary<string, object?> pending = [];
        OptionButtonItem.Create(tab, TranslationStrings.BetterSetting_Action_ImportPreset, TranslationStrings.BetterSetting_Action_Paste, () =>
        {
            int count = OptionPresetItem.Import(pending);
            pending = [];
            OptionButtonItem.Notify(TranslationStrings.BetterSetting_Action_ImportPreset_Done.Format(count.ToString()));
        }, TranslationStrings.BetterSetting_Action_Confirm, () =>
        {
            if (PresetShareCodec.TryDecode(ClipboardHelper.GetClipboardString(), out pending))
                return true;

            OptionButtonItem.Notify(TranslationStrings.BetterSetting_Action_ImportPreset_Invalid.LocalizedString);
            return false;
        });
    }

    // Initialize settings
    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
    [HarmonyPostfix]
    private static void GameSettingMenu_Start_Postfix(GameSettingMenu __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions))
            return;

        SetupSettings();

        // Adjust menu layout
        __instance.gameObject.transform.SetLocalY(-0.1f);
        GameObject PanelSprite = __instance.gameObject.transform.Find("PanelSprite").gameObject;
        if (PanelSprite != null)
        {
            PanelSprite.transform.SetLocalY(-0.32f);
            PanelSprite.transform.localScale = new Vector3(PanelSprite.transform.localScale.x, 0.625f);
        }

        // Clear hover effects
        __instance.GamePresetsButton.OnMouseOver.RemoveAllListeners();
        __instance.GameSettingsButton.OnMouseOver.RemoveAllListeners();
        __instance.RoleSettingsButton.OnMouseOver.RemoveAllListeners();

        // Configure tab behavior based on game mode and host status
        BetterSettingsTab.TabButton.transform.localPosition = BetterSettingsTab.TabButton.transform.localPosition - new Vector3(0f, 1.265f, 0f);

        if (!GameState.IsHideNSeek && GameState.IsHost)
        {
            __instance.ChangeTab(1, false); // Show game settings tab for hosts
        }
        else if (GameState.IsHost)
        {
            // Hide & Seek host: disable role button but keep visible
            __instance.RoleSettingsButton.gameObject.SetActive(true);
            __instance.RoleSettingsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.RoleSettingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.ChangeTab(1, false);
        }
        else
        {
            // Non-hosts: disable all vanilla tabs and show custom tab
            __instance.GamePresetsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.GameSettingsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.RoleSettingsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.GamePresetsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.GameSettingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.RoleSettingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.ChangeTab(3, false); // Switch to custom tab
        }
    }

    // Handle tab switching to show/hide custom settings tab
    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.ChangeTab))]
    [HarmonyPrefix]
    private static void GameSettingMenu_ChangeTab_Prefix(GameSettingMenu __instance, [HarmonyArgument(0)] int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions))
            return;

        if (BetterSettingsTab == null || BetterSettingsTab.AUTab == null || BetterSettingsTab.TabButton == null)
            return;

        BetterSettingsTab.AUTab.gameObject.SetActive(false);
        BetterSettingsTab.TabButton.SelectButton(false);

        // Show custom tab when selected (tab 3)
        if (previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick || !previewOnly)
        {
            switch (tabNum)
            {
                case 3:
                    BetterSettingsTab.AUTab.gameObject.SetActive(true);
                    BetterSettingsTab.TabButton.SelectButton(true);
                    __instance.MenuDescriptionText.text = BetterSettingsTab.Description;
                    break;
            }
        }
    }

    // Prevent vanilla settings from being created in custom tab
    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.CreateSettings))]
    [HarmonyPrefix]
    private static bool GameOptionsMenu_CreateSettings_Prefix(GameOptionsMenu __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions)) return true;

        // Skip creation if this is our custom tab
        if (__instance == BetterSettingsTab.AUTab)
        {
            return false;
        }

        return true;
    }

    // Make OptionsConsole usable by anyone (not just host)
    [HarmonyPatch(typeof(OptionsConsole), nameof(OptionsConsole.CanUse))]
    [HarmonyPrefix]
    private static void OptionsConsole_CanUse_Prefix(OptionsConsole __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions))
            return;

        __instance.HostOnly = false; // Allow non-hosts to use settings console
    }
}