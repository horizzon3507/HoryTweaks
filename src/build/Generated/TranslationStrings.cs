// auto-generated
using BetterAmongUs.Modules;

namespace BetterAmongUs.Generated;

/// <summary>
/// Provides strongly-typed translation keys for BAU.
/// Each constant represents a translation key that can be used with the Translator.
/// </summary>
public static class TranslationStrings
{
    /// <summary>
    /// Represents a translation key with the ability to get the localized string.
    /// </summary>
    public readonly struct TranslationString(string key)
    {
        public readonly string Key = key;
        public string LocalizedString => Translator.GetString(this);
        public override string ToString() => LocalizedString;
        public string Format(params string[] strings)
        {
            return string.Format(LocalizedString, args: strings);
        }
        public string Format(params TranslationString[] translationStrings)
        {
            var stringArgs = translationStrings.Select(ts => ts.LocalizedString).ToArray();
            return string.Format(LocalizedString, stringArgs);
        }
        public string Format(params object[] args)
        {
            var stringArgs = args.Select(arg => arg.ToString()).ToArray();
            return string.Format(LocalizedString, stringArgs);
        }
    }

    /// <summary>
    /// Base Translation: HT
    /// </summary>
    public static readonly TranslationString BAU = new("BAU");

    /// <summary>
    /// Base Translation: HoryTweaks
    /// </summary>
    public static readonly TranslationString BetterAmongUs = new("BetterAmongUs");

    /// <summary>
    /// Base Translation: ♻
    /// </summary>
    public static readonly TranslationString BAUMark = new("BAUMark");

    /// <summary>
    /// Base Translation: ⚠
    /// </summary>
    public static readonly TranslationString WarningIcon = new("WarningIcon");

    /// <summary>
    /// Base Translation: Host
    /// </summary>
    public static readonly TranslationString Host = new("Host");

    /// <summary>
    /// Base Translation: Kills
    /// </summary>
    public static readonly TranslationString Kills = new("Kills");

    /// <summary>
    /// Base Translation: Tasks
    /// </summary>
    public static readonly TranslationString Tasks = new("Tasks");

    /// <summary>
    /// Base Translation: Alive
    /// </summary>
    public static readonly TranslationString Alive = new("Alive");

    /// <summary>
    /// Base Translation: Dead
    /// </summary>
    public static readonly TranslationString Dead = new("Dead");

    /// <summary>
    /// Base Translation: D/C
    /// </summary>
    public static readonly TranslationString DC = new("DC");

    /// <summary>
    /// Base Translation: Ping
    /// </summary>
    public static readonly TranslationString Ping = new("Ping");

    /// <summary>
    /// Base Translation: Timer
    /// </summary>
    public static readonly TranslationString Timer = new("Timer");

    /// <summary>
    /// Base Translation: System Message
    /// </summary>
    public static readonly TranslationString SystemMessage = new("SystemMessage");

    /// <summary>
    /// Base Translation: System Notification
    /// </summary>
    public static readonly TranslationString SystemNotification = new("SystemNotification");

    /// <summary>
    /// Base Translation: {0} does not support\n&lt;b&gt;Modded Lobbies&lt;/b&gt;
    /// </summary>
    public static readonly TranslationString ModdedLobbyMsg = new("ModdedLobbyMsg");

    /// <summary>
    /// Base Translation: Host: {0}
    /// </summary>
    public static readonly TranslationString HostInMeeting = new("HostInMeeting");

    /// <summary>
    /// Base Translation: Preset {0}
    /// </summary>
    public static readonly TranslationString Setting_Preset = new("Setting.Preset");

    /// <summary>
    /// Base Translation: Presets
    /// </summary>
    public static readonly TranslationString Setting_Presets = new("Setting.Presets");

    /// <summary>
    /// Base Translation: Search settings...
    /// </summary>
    public static readonly TranslationString SettingsSearch_Placeholder = new("SettingsSearch.Placeholder");

    /// <summary>
    /// Base Translation: Loading
    /// </summary>
    public static readonly TranslationString Player_Loading = new("Player.Loading");

    /// <summary>
    /// Base Translation: No Friend Code
    /// </summary>
    public static readonly TranslationString Player_NoFriendCode = new("Player.NoFriendCode");

    /// <summary>
    /// Base Translation: Platform Hidden
    /// </summary>
    public static readonly TranslationString Player_PlatformHidden = new("Player.PlatformHidden");

    /// <summary>
    /// Base Translation: Better-User
    /// </summary>
    public static readonly TranslationString Player_BetterUser = new("Player.BetterUser");

    /// <summary>
    /// Base Translation: Hory-User
    /// </summary>
    public static readonly TranslationString Player_HoryUser = new("Player.HoryUser");

    /// <summary>
    /// Base Translation: {0} Left the game!
    /// </summary>
    public static readonly TranslationString DisconnectReason_Left = new("DisconnectReason.Left");

    /// <summary>
    /// Base Translation: {0} Disconnected!
    /// </summary>
    public static readonly TranslationString DisconnectReason_Disconnect = new("DisconnectReason.Disconnect");

    /// <summary>
    /// Base Translation: {0} Was kicked by {1}!
    /// </summary>
    public static readonly TranslationString DisconnectReason_Kicked = new("DisconnectReason.Kicked");

    /// <summary>
    /// Base Translation: {0} Was banned by {1}!
    /// </summary>
    public static readonly TranslationString DisconnectReason_Banned = new("DisconnectReason.Banned");

    /// <summary>
    /// Base Translation: {0} Was banned by Innersloth Anti-Cheat!
    /// </summary>
    public static readonly TranslationString DisconnectReason_Cheater = new("DisconnectReason.Cheater");

    /// <summary>
    /// Base Translation: {0} Was kicked due to an error!
    /// </summary>
    public static readonly TranslationString DisconnectReason_Error = new("DisconnectReason.Error");

    /// <summary>
    /// Base Translation: {0} Left the game due to unknown reason?
    /// </summary>
    public static readonly TranslationString DisconnectReason_Unknown = new("DisconnectReason.Unknown");

    /// <summary>
    /// Base Translation: Left The Game
    /// </summary>
    public static readonly TranslationString DisconnectReasonMeeting_Left = new("DisconnectReasonMeeting.Left");

    /// <summary>
    /// Base Translation: Disconnected
    /// </summary>
    public static readonly TranslationString DisconnectReasonMeeting_Disconnect = new("DisconnectReasonMeeting.Disconnect");

    /// <summary>
    /// Base Translation: Kicked By Host
    /// </summary>
    public static readonly TranslationString DisconnectReasonMeeting_Kicked = new("DisconnectReasonMeeting.Kicked");

    /// <summary>
    /// Base Translation: Banned By Host
    /// </summary>
    public static readonly TranslationString DisconnectReasonMeeting_Banned = new("DisconnectReasonMeeting.Banned");

    /// <summary>
    /// Base Translation: Banned By Server
    /// </summary>
    public static readonly TranslationString DisconnectReasonMeeting_Cheater = new("DisconnectReasonMeeting.Cheater");

    /// <summary>
    /// Base Translation: Auto Rejoin
    /// </summary>
    public static readonly TranslationString Rejoin_AutoRejoin = new("Rejoin.AutoRejoin");

    /// <summary>
    /// Base Translation: Press Back to cancel
    /// </summary>
    public static readonly TranslationString Rejoin_CancelHint = new("Rejoin.CancelHint");

    /// <summary>
    /// Base Translation: Could not reconnect to lobby {0}.
    /// </summary>
    public static readonly TranslationString Rejoin_Failed = new("Rejoin.Failed");

    /// <summary>
    /// Base Translation: Reconnecting to lobby {0}... ({1}s)
    /// </summary>
    public static readonly TranslationString Rejoin_Reconnecting = new("Rejoin.Reconnecting");

    /// <summary>
    /// Base Translation: Welcome To {0}
    /// </summary>
    public static readonly TranslationString WelcomeMsg_WelcomeToBAU = new("WelcomeMsg.WelcomeToBAU");

    /// <summary>
    /// Base Translation: Thanks for downloading!
    /// </summary>
    public static readonly TranslationString WelcomeMsg_ThanksForDownloading = new("WelcomeMsg.ThanksForDownloading");

    /// <summary>
    /// Base Translation: &lt;color=#ffffbe&gt;{0}&lt;/color&gt; improves the vanilla Among Us experience with client-side features like {1}, host tools and more. You can play with people using the vanilla game.
    /// </summary>
    public static readonly TranslationString WelcomeMsg_BAUDescription1 = new("WelcomeMsg.BAUDescription1");

    /// <summary>
    /// Base Translation: Tweaks
    /// </summary>
    public static readonly TranslationString BetterOption = new("BetterOption");

    /// <summary>
    /// Base Translation: Next &gt;
    /// </summary>
    public static readonly TranslationString BetterOption_Next = new("BetterOption.Next");

    /// <summary>
    /// Base Translation: &lt; Prev
    /// </summary>
    public static readonly TranslationString BetterOption_Previous = new("BetterOption.Previous");

    /// <summary>
    /// Base Translation: &lt;color=#4f92ff&gt;Send Better RPC&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString BetterOption_SendBetterRpc = new("BetterOption.SendBetterRpc");

    /// <summary>
    /// Base Translation: &lt;color=#4f92ff&gt;Better Notifications&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString BetterOption_BetterNotifications = new("BetterOption.BetterNotifications");

    /// <summary>
    /// Base Translation: Force System language
    /// </summary>
    public static readonly TranslationString BetterOption_ForceOwnLanguage = new("BetterOption.ForceOwnLanguage");

    /// <summary>
    /// Base Translation: Chat Dark Mode
    /// </summary>
    public static readonly TranslationString BetterOption_ChatDarkMode = new("BetterOption.ChatDarkMode");

    /// <summary>
    /// Base Translation: Hide Console (restart required)
    /// </summary>
    public static readonly TranslationString BetterOption_HideConsole = new("BetterOption.HideConsole");

    /// <summary>
    /// Base Translation: Show Lobby Info
    /// </summary>
    public static readonly TranslationString BetterOption_LobbyInfo = new("BetterOption.LobbyInfo");

    /// <summary>
    /// Base Translation: Less Info
    /// </summary>
    public static readonly TranslationString BetterOption_LessInfo = new("BetterOption.LessInfo");

    /// <summary>
    /// Base Translation: Disable Lobby Theme
    /// </summary>
    public static readonly TranslationString BetterOption_LobbyTheme = new("BetterOption.LobbyTheme");

    /// <summary>
    /// Base Translation: Unlock FPS
    /// </summary>
    public static readonly TranslationString BetterOption_UnlockFPS = new("BetterOption.UnlockFPS");

    /// <summary>
    /// Base Translation: Show FPS
    /// </summary>
    public static readonly TranslationString BetterOption_ShowFPS = new("BetterOption.ShowFPS");

    /// <summary>
    /// Base Translation: Vent Color Groups
    /// </summary>
    public static readonly TranslationString BetterOption_VentColorGroups = new("BetterOption.VentColorGroups");

    /// <summary>
    /// Base Translation: Minimap Icons
    /// </summary>
    public static readonly TranslationString BetterOption_MinimapIcons = new("BetterOption.MinimapIcons");

    /// <summary>
    /// Base Translation: Better Minimap Colors
    /// </summary>
    public static readonly TranslationString BetterOption_BetterMinimapColors = new("BetterOption.BetterMinimapColors");

    /// <summary>
    /// Base Translation: Better Colorblind Text
    /// </summary>
    public static readonly TranslationString BetterOption_BetterColorblindText = new("BetterOption.BetterColorblindText");

    /// <summary>
    /// Base Translation: Colorblind Text On Top
    /// </summary>
    public static readonly TranslationString BetterOption_ColorblindTextOnTop = new("BetterOption.ColorblindTextOnTop");

    /// <summary>
    /// Base Translation: Compress Setting Files
    /// </summary>
    public static readonly TranslationString BetterOption_CompressSettingFiles = new("BetterOption.CompressSettingFiles");

    /// <summary>
    /// Base Translation: Open Save Data
    /// </summary>
    public static readonly TranslationString BetterOption_SaveData = new("BetterOption.SaveData");

    /// <summary>
    /// Base Translation: Switch To Vanilla
    /// </summary>
    public static readonly TranslationString BetterOption_ToVanilla = new("BetterOption.ToVanilla");

    /// <summary>
    /// Base Translation: Moderation Center
    /// </summary>
    public static readonly TranslationString BetterOption_Moderation = new("BetterOption.Moderation");

    /// <summary>
    /// Base Translation: &lt; Back
    /// </summary>
    public static readonly TranslationString ModerationCenter_Back = new("ModerationCenter.Back");

    /// <summary>
    /// Base Translation: Refresh
    /// </summary>
    public static readonly TranslationString ModerationCenter_Refresh = new("ModerationCenter.Refresh");

    /// <summary>
    /// Base Translation: Kick
    /// </summary>
    public static readonly TranslationString ModerationCenter_Kick = new("ModerationCenter.Kick");

    /// <summary>
    /// Base Translation: Ban
    /// </summary>
    public static readonly TranslationString ModerationCenter_Ban = new("ModerationCenter.Ban");

    /// <summary>
    /// Base Translation: Confirm kick
    /// </summary>
    public static readonly TranslationString ModerationCenter_ConfirmKick = new("ModerationCenter.ConfirmKick");

    /// <summary>
    /// Base Translation: Confirm ban
    /// </summary>
    public static readonly TranslationString ModerationCenter_ConfirmBan = new("ModerationCenter.ConfirmBan");

    /// <summary>
    /// Base Translation: Select a player, then choose Kick or Ban. Every action asks for confirmation.
    /// </summary>
    public static readonly TranslationString ModerationCenter_SelectPlayer = new("ModerationCenter.SelectPlayer");

    /// <summary>
    /// Base Translation: Selected {0}. Choose Kick or Ban, then click it again to confirm.
    /// </summary>
    public static readonly TranslationString ModerationCenter_Selected = new("ModerationCenter.Selected");

    /// <summary>
    /// Base Translation: Click Confirm kick to kick {0}. Select another player to cancel.
    /// </summary>
    public static readonly TranslationString ModerationCenter_PendingKick = new("ModerationCenter.PendingKick");

    /// <summary>
    /// Base Translation: Click Confirm ban to ban {0}. Bans are also added to the player ban list when it is enabled.
    /// </summary>
    public static readonly TranslationString ModerationCenter_PendingBan = new("ModerationCenter.PendingBan");

    /// <summary>
    /// Base Translation: {0} will be kicked.
    /// </summary>
    public static readonly TranslationString ModerationCenter_Kicked = new("ModerationCenter.Kicked");

    /// <summary>
    /// Base Translation: {0} will be banned.
    /// </summary>
    public static readonly TranslationString ModerationCenter_Banned = new("ModerationCenter.Banned");

    /// <summary>
    /// Base Translation: has been {0} by the host from the moderation center!
    /// </summary>
    public static readonly TranslationString ModerationCenter_ActionReason = new("ModerationCenter.ActionReason");

    /// <summary>
    /// Base Translation: Only the host can kick or ban players.
    /// </summary>
    public static readonly TranslationString ModerationCenter_NotHost = new("ModerationCenter.NotHost");

    /// <summary>
    /// Base Translation: Host or join a lobby to manage players. The ban lists and history are still available.
    /// </summary>
    public static readonly TranslationString ModerationCenter_NotInGame = new("ModerationCenter.NotInGame");

    /// <summary>
    /// Base Translation: Wait until the game has finished starting.
    /// </summary>
    public static readonly TranslationString ModerationCenter_GameStarting = new("ModerationCenter.GameStarting");

    /// <summary>
    /// Base Translation: The game has ended. Moderate players from the lobby.
    /// </summary>
    public static readonly TranslationString ModerationCenter_GameEnded = new("ModerationCenter.GameEnded");

    /// <summary>
    /// Base Translation: That player is no longer in the game.
    /// </summary>
    public static readonly TranslationString ModerationCenter_TargetMissing = new("ModerationCenter.TargetMissing");

    /// <summary>
    /// Base Translation: Practice dummies cannot be moderated.
    /// </summary>
    public static readonly TranslationString ModerationCenter_TargetIsDummy = new("ModerationCenter.TargetIsDummy");

    /// <summary>
    /// Base Translation: You cannot kick or ban yourself.
    /// </summary>
    public static readonly TranslationString ModerationCenter_TargetIsSelf = new("ModerationCenter.TargetIsSelf");

    /// <summary>
    /// Base Translation: The host cannot be kicked or banned.
    /// </summary>
    public static readonly TranslationString ModerationCenter_TargetIsHost = new("ModerationCenter.TargetIsHost");

    /// <summary>
    /// Base Translation: Wait until that player has finished loading.
    /// </summary>
    public static readonly TranslationString ModerationCenter_TargetNotReady = new("ModerationCenter.TargetNotReady");

    /// <summary>
    /// Base Translation: No other players are in the game yet.
    /// </summary>
    public static readonly TranslationString ModerationCenter_NoPlayers = new("ModerationCenter.NoPlayers");

    /// <summary>
    /// Base Translation: Player bans
    /// </summary>
    public static readonly TranslationString ModerationCenter_PlayerList = new("ModerationCenter.PlayerList");

    /// <summary>
    /// Base Translation: Name bans
    /// </summary>
    public static readonly TranslationString ModerationCenter_NameList = new("ModerationCenter.NameList");

    /// <summary>
    /// Base Translation: Chat bans
    /// </summary>
    public static readonly TranslationString ModerationCenter_ChatList = new("ModerationCenter.ChatList");

    /// <summary>
    /// Base Translation: {0} has {1} entries: friend codes and hashed IDs, one player per line. Bans from the game are added automatically. Matching players are banned when they join.
    /// </summary>
    public static readonly TranslationString ModerationCenter_PlayerListInfo = new("ModerationCenter.PlayerListInfo");

    /// <summary>
    /// Base Translation: {0} has {1} entries: regex patterns, one per line. Players whose name matches are banned when they join.
    /// </summary>
    public static readonly TranslationString ModerationCenter_NameListInfo = new("ModerationCenter.NameListInfo");

    /// <summary>
    /// Base Translation: {0} has {1} entries: regex patterns, one per line. Players whose chat message matches are kicked or banned by the host settings.
    /// </summary>
    public static readonly TranslationString ModerationCenter_ChatListInfo = new("ModerationCenter.ChatListInfo");

    /// <summary>
    /// Base Translation: The file is open in your text editor. Lines starting with # or // are comments. Saved changes apply on the next check.
    /// </summary>
    public static readonly TranslationString ModerationCenter_ListOpened = new("ModerationCenter.ListOpened");

    /// <summary>
    /// Base Translation: Edit the file in the Better_Data folder with a text editor. It opens from here in the lobby or main menu.
    /// </summary>
    public static readonly TranslationString ModerationCenter_ListOpenBlocked = new("ModerationCenter.ListOpenBlocked");

    /// <summary>
    /// Base Translation: {0} was not found. It is created in Better_Data when the mod starts.
    /// </summary>
    public static readonly TranslationString ModerationCenter_ListMissing = new("ModerationCenter.ListMissing");

    /// <summary>
    /// Base Translation: Kicks and bans this session
    /// </summary>
    public static readonly TranslationString ModerationCenter_HistoryTitle = new("ModerationCenter.HistoryTitle");

    /// <summary>
    /// Base Translation: No kicks or bans yet.
    /// </summary>
    public static readonly TranslationString ModerationCenter_HistoryEmpty = new("ModerationCenter.HistoryEmpty");

    /// <summary>
    /// Base Translation: &lt;color=#aaaaaa&gt;{0}&lt;/color&gt; {1} &lt;color=#ffff00&gt;{2}&lt;/color&gt; {3}
    /// </summary>
    public static readonly TranslationString ModerationCenter_HistoryEntry = new("ModerationCenter.HistoryEntry");

    /// <summary>
    /// Base Translation: Tweaks
    /// </summary>
    public static readonly TranslationString BetterSetting = new("BetterSetting");

    /// <summary>
    /// Base Translation: Edit HoryTweaks settings for your lobby and gameplay.
    /// </summary>
    public static readonly TranslationString BetterSetting_Description = new("BetterSetting.Description");

    /// <summary>
    /// Base Translation: Set To:
    /// </summary>
    public static readonly TranslationString BetterSetting_SetTo = new("BetterSetting.SetTo");

    /// <summary>
    /// Base Translation: On
    /// </summary>
    public static readonly TranslationString BetterSetting_State_On = new("BetterSetting.State.On");

    /// <summary>
    /// Base Translation: Off
    /// </summary>
    public static readonly TranslationString BetterSetting_State_Off = new("BetterSetting.State.Off");

    /// <summary>
    /// Base Translation: Confirm?
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_Confirm = new("BetterSetting.Action.Confirm");

    /// <summary>
    /// Base Translation: Restore default settings
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_RestoreDefaults = new("BetterSetting.Action.RestoreDefaults");

    /// <summary>
    /// Base Translation: Reset
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_Reset = new("BetterSetting.Action.Reset");

    /// <summary>
    /// Base Translation: Restored {0} settings to their defaults
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_RestoreDefaults_Done = new("BetterSetting.Action.RestoreDefaults.Done");

    /// <summary>
    /// Base Translation: Copy preset to clipboard
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_ExportPreset = new("BetterSetting.Action.ExportPreset");

    /// <summary>
    /// Base Translation: Copy
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_Copy = new("BetterSetting.Action.Copy");

    /// <summary>
    /// Base Translation: Preset code copied to the clipboard
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_ExportPreset_Done = new("BetterSetting.Action.ExportPreset.Done");

    /// <summary>
    /// Base Translation: Paste preset from clipboard
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_ImportPreset = new("BetterSetting.Action.ImportPreset");

    /// <summary>
    /// Base Translation: Paste
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_Paste = new("BetterSetting.Action.Paste");

    /// <summary>
    /// Base Translation: Applied {0} settings from the pasted preset
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_ImportPreset_Done = new("BetterSetting.Action.ImportPreset.Done");

    /// <summary>
    /// Base Translation: The clipboard does not contain a valid preset code
    /// </summary>
    public static readonly TranslationString BetterSetting_Action_ImportPreset_Invalid = new("BetterSetting.Action.ImportPreset.Invalid");

    /// <summary>
    /// Base Translation: System Settings
    /// </summary>
    public static readonly TranslationString BetterSetting_MainHeader_System = new("BetterSetting.MainHeader.System");

    /// <summary>
    /// Base Translation: Host Tools
    /// </summary>
    public static readonly TranslationString BetterSetting_MainHeader_HostTools = new("BetterSetting.MainHeader.HostTools");

    /// <summary>
    /// Base Translation: Gameplay Settings
    /// </summary>
    public static readonly TranslationString BetterSetting_MainHeader_Gameplay = new("BetterSetting.MainHeader.Gameplay");

    /// <summary>
    /// Base Translation: Host Only
    /// </summary>
    public static readonly TranslationString BetterSetting_TextHeader_HostOnly = new("BetterSetting.TextHeader.HostOnly");

    /// <summary>
    /// Base Translation: Host moderation cooldown
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickCooldown = new("BetterSetting.Setting.KickCooldown");

    /// <summary>
    /// Base Translation: Kick players with invalid friend codes
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_InvalidFriendCode = new("BetterSetting.Setting.InvalidFriendCode");

    /// <summary>
    /// Base Translation: Cancel invalid sabotage attempts
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_CancelInvalidSabotage = new("BetterSetting.Setting.CancelInvalidSabotage");

    /// <summary>
    /// Base Translation: Use ban player list
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanPlayerList = new("BetterSetting.Setting.UseBanPlayerList");

    /// <summary>
    /// Base Translation: Use ban name list
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanNameList = new("BetterSetting.Setting.UseBanNameList");

    /// <summary>
    /// Base Translation: Use ban chat list
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanChatList = new("BetterSetting.Setting.UseBanChatList");

    /// <summary>
    /// Base Translation: Only in lobby
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanChatListOnlyLobby = new("BetterSetting.Setting.UseBanChatListOnlyLobby");

    /// <summary>
    /// Base Translation: Ban
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanChatListBan = new("BetterSetting.Setting.UseBanChatListBan");

    /// <summary>
    /// Base Translation: Enforce a minimum player level
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickLevel = new("BetterSetting.Setting.KickLevel");

    /// <summary>
    /// Base Translation: Minimum player level
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickLevelBelow = new("BetterSetting.Setting.KickLevelBelow");

    /// <summary>
    /// Base Translation: Minimum players required to enforce level
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickLevelBelowMinimumPlayers = new("BetterSetting.Setting.KickLevelBelowMinimumPlayers");

    /// <summary>
    /// Base Translation: Limit RPCs sent per second
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_RpcRateLimiting = new("BetterSetting.Setting.RpcRateLimiting");

    /// <summary>
    /// Base Translation: RPC limit per second
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_RateLimit = new("BetterSetting.Setting.RateLimit");

    /// <summary>
    /// Base Translation: Each preset keeps its own copy of the settings below. Name the selected preset with /presetname, and share it with the Copy and Paste rows.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_Presets_Description = new("BetterSetting.Setting.Presets.Description");

    /// <summary>
    /// Base Translation: Minimum time between moderation actions taken by the host, such as kicks and bans.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickCooldown_Description = new("BetterSetting.Setting.KickCooldown.Description");

    /// <summary>
    /// Base Translation: Kick players who join without a valid friend code.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_InvalidFriendCode_Description = new("BetterSetting.Setting.InvalidFriendCode.Description");

    /// <summary>
    /// Base Translation: Ban players whose friend code or account ID is listed in the ban player list file.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanPlayerList_Description = new("BetterSetting.Setting.UseBanPlayerList.Description");

    /// <summary>
    /// Base Translation: Ban players whose name matches a pattern in the ban name list file.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanNameList_Description = new("BetterSetting.Setting.UseBanNameList.Description");

    /// <summary>
    /// Base Translation: Kick or ban players whose chat message matches a pattern in the ban chat list file.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_UseBanChatList_Description = new("BetterSetting.Setting.UseBanChatList.Description");

    /// <summary>
    /// Base Translation: Kick players in the lobby whose level is below the minimum player level.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickLevel_Description = new("BetterSetting.Setting.KickLevel.Description");

    /// <summary>
    /// Base Translation: The level check only runs once the lobby has at least this many players.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_KickLevelBelowMinimumPlayers_Description = new("BetterSetting.Setting.KickLevelBelowMinimumPlayers.Description");

    /// <summary>
    /// Base Translation: Ignore RPCs from players who send more than the limit per second.
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_RpcRateLimiting_Description = new("BetterSetting.Setting.RpcRateLimiting.Description");

    /// <summary>
    /// Base Translation: Disable sabotages for dead
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_DisableSabotagesForDead = new("BetterSetting.Setting.DisableSabotagesForDead");

    /// <summary>
    /// Base Translation: Disable sabotages
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_DisableSabotages = new("BetterSetting.Setting.DisableSabotages");

    /// <summary>
    /// Base Translation: Remove pet on death
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_RemovePetOnDeath = new("BetterSetting.Setting.RemovePetOnDeath");

    /// <summary>
    /// Base Translation: # Seekers
    /// </summary>
    public static readonly TranslationString BetterSetting_Setting_HideAndSeekImpNum = new("BetterSetting.Setting.HideAndSeekImpNum");

    /// <summary>
    /// Base Translation: Seeker
    /// </summary>
    public static readonly TranslationString BetterSetting_TempSetting_HideAndSeekImpNum = new("BetterSetting.TempSetting.HideAndSeekImpNum");

    /// <summary>
    /// Base Translation: Copy recent chat messages to the clipboard
    /// </summary>
    public static readonly TranslationString Chat_Copy_Description = new("Chat.Copy.Description");

    /// <summary>
    /// Base Translation: Copied &lt;color=#e0b700&gt;{0}&lt;/color&gt; chat messages to the clipboard.
    /// </summary>
    public static readonly TranslationString Chat_Copy_Done = new("Chat.Copy.Done");

    /// <summary>
    /// Base Translation: No chat messages to copy.
    /// </summary>
    public static readonly TranslationString Chat_Copy_Empty = new("Chat.Copy.Empty");

    /// <summary>
    /// Base Translation: Extended Chat History
    /// </summary>
    public static readonly TranslationString Chat_ExtendedHistory = new("Chat.ExtendedHistory");

    /// <summary>
    /// Base Translation: Chat Timestamps
    /// </summary>
    public static readonly TranslationString Chat_Timestamps = new("Chat.Timestamps");

    /// <summary>
    /// Base Translation: Banned
    /// </summary>
    public static readonly TranslationString HostTools_Ban = new("HostTools.Ban");

    /// <summary>
    /// Base Translation: Kicked
    /// </summary>
    public static readonly TranslationString HostTools_Kick = new("HostTools.Kick");

    /// <summary>
    /// Base Translation: {0}! Reason: &lt;color=#fc0000&gt;{1}&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString HostTools_KickMessage = new("HostTools.KickMessage");

    /// <summary>
    /// Base Translation: has been {0} for being on the banned player list!
    /// </summary>
    public static readonly TranslationString HostTools_BanPlayerListMessage = new("HostTools.BanPlayerListMessage");

    /// <summary>
    /// Base Translation: has been {0} for having a name on the banned name list!
    /// </summary>
    public static readonly TranslationString HostTools_BanNameListMessage = new("HostTools.BanNameListMessage");

    /// <summary>
    /// Base Translation: Invalid friend code
    /// </summary>
    public static readonly TranslationString HostTools_InvalidFriendCode = new("HostTools.InvalidFriendCode");

    /// <summary>
    /// Base Translation: Failed to initialize before the timeout expired
    /// </summary>
    public static readonly TranslationString HostTools_InitializeTimeout = new("HostTools.InitializeTimeout");

    /// <summary>
    /// Base Translation: Host transferred to {0} due to AFK
    /// </summary>
    public static readonly TranslationString HostTransfer_AfkSuccess = new("HostTransfer.AfkSuccess");

    /// <summary>
    /// Base Translation: Transfer the host role to a player
    /// </summary>
    public static readonly TranslationString HostTransfer_Command_Description = new("HostTransfer.Command.Description");

    /// <summary>
    /// Base Translation: Cannot transfer host after the game ended
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_GameEnded = new("HostTransfer.Denied.GameEnded");

    /// <summary>
    /// Base Translation: Cannot transfer host while the game is starting
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_GameStarting = new("HostTransfer.Denied.GameStarting");

    /// <summary>
    /// Base Translation: No eligible player to receive host
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_NoEligibleTarget = new("HostTransfer.Denied.NoEligibleTarget");

    /// <summary>
    /// Base Translation: Only the host can transfer host
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_NotHost = new("HostTransfer.Denied.NotHost");

    /// <summary>
    /// Base Translation: Cannot transfer host outside a game
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_NotInGame = new("HostTransfer.Denied.NotInGame");

    /// <summary>
    /// Base Translation: AFK host transfer only works in a lobby
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_NotInLobby = new("HostTransfer.Denied.NotInLobby");

    /// <summary>
    /// Base Translation: Cannot transfer host to a dummy player
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_TargetIsDummy = new("HostTransfer.Denied.TargetIsDummy");

    /// <summary>
    /// Base Translation: Target is already the host
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_TargetIsHost = new("HostTransfer.Denied.TargetIsHost");

    /// <summary>
    /// Base Translation: Cannot transfer host to yourself
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_TargetIsSelf = new("HostTransfer.Denied.TargetIsSelf");

    /// <summary>
    /// Base Translation: Target player not found
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_TargetMissing = new("HostTransfer.Denied.TargetMissing");

    /// <summary>
    /// Base Translation: Target player is not ready
    /// </summary>
    public static readonly TranslationString HostTransfer_Denied_TargetNotReady = new("HostTransfer.Denied.TargetNotReady");

    /// <summary>
    /// Base Translation: You are now the host
    /// </summary>
    public static readonly TranslationString HostTransfer_Received = new("HostTransfer.Received");

    /// <summary>
    /// Base Translation: AFK Minutes
    /// </summary>
    public static readonly TranslationString HostTransfer_Setting_AfkMinutes = new("HostTransfer.Setting.AfkMinutes");

    /// <summary>
    /// Base Translation: Transfer Host On AFK
    /// </summary>
    public static readonly TranslationString HostTransfer_Setting_OnAfk = new("HostTransfer.Setting.OnAfk");

    /// <summary>
    /// Base Translation: Automatically transfers host to the player who has been in the lobby longest when you are AFK
    /// </summary>
    public static readonly TranslationString HostTransfer_Setting_OnAfk_Description = new("HostTransfer.Setting.OnAfk.Description");

    /// <summary>
    /// Base Translation: Transferred host to {0}
    /// </summary>
    public static readonly TranslationString HostTransfer_Success = new("HostTransfer.Success");

    /// <summary>
    /// Base Translation: Game Summary
    /// </summary>
    public static readonly TranslationString GameSummary = new("GameSummary");

    /// <summary>
    /// Base Translation: Hiders
    /// </summary>
    public static readonly TranslationString Game_Summary_Hiders = new("Game.Summary.Hiders");

    /// <summary>
    /// Base Translation: Seekers
    /// </summary>
    public static readonly TranslationString Game_Summary_Seekers = new("Game.Summary.Seekers");

    /// <summary>
    /// Base Translation: Won
    /// </summary>
    public static readonly TranslationString Game_Summary_Won = new("Game.Summary.Won");

    /// <summary>
    /// Base Translation: By
    /// </summary>
    public static readonly TranslationString Game_Summary_By = new("Game.Summary.By");

    /// <summary>
    /// Base Translation: Tasks Completion
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_TasksCompletion = new("Game.Summary.Result.TasksCompletion");

    /// <summary>
    /// Base Translation: Imposters Voted Out
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_ImpostersVotedOut = new("Game.Summary.Result.ImpostersVotedOut");

    /// <summary>
    /// Base Translation: Impostors Disconnected
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_ImpostorsDisconnected = new("Game.Summary.Result.ImpostorsDisconnected");

    /// <summary>
    /// Base Translation: Crew Outnumbered
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_CrewOutnumbered = new("Game.Summary.Result.CrewOutnumbered");

    /// <summary>
    /// Base Translation: Sabotage
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_Sabotage = new("Game.Summary.Result.Sabotage");

    /// <summary>
    /// Base Translation: Cremates Disconnected
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_CrematesDisconnected = new("Game.Summary.Result.CrematesDisconnected");

    /// <summary>
    /// Base Translation: Time Out
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_TimeOut = new("Game.Summary.Result.TimeOut");

    /// <summary>
    /// Base Translation: No Survivors
    /// </summary>
    public static readonly TranslationString Game_Summary_Result_NoSurvivors = new("Game.Summary.Result.NoSurvivors");

    /// <summary>
    /// Base Translation: Get help with commands
    /// </summary>
    public static readonly TranslationString Command_Help_Description = new("Command.Help.Description");

    /// <summary>
    /// Base Translation: &lt;color=#ffffbe&gt;{0}&lt;/color&gt; improves your vanilla Among Us experience with useful client-side features.\nOpen the pause menu to access more options and game settings.\nUse &lt;color=#e0b700&gt;/commands&lt;/color&gt; to see every available command.\n\nFeatures:\n- Host-side gameplay validation and manual moderation.\n- Additional options for hosts.\n- Enhanced settings to customize your game.\n- Commands to manage and improve your experience.\n- Client-side improvements and quality-of-life features.
    /// </summary>
    public static readonly TranslationString Command_Help_Body = new("Command.Help.Body");

    /// <summary>
    /// Base Translation: List all available commands
    /// </summary>
    public static readonly TranslationString Command_Commands_Description = new("Command.Commands.Description");

    /// <summary>
    /// Base Translation: Save the full log and a diagnostic report to the desktop
    /// </summary>
    public static readonly TranslationString Command_Dump_Description = new("Command.Dump.Description");

    /// <summary>
    /// Base Translation: Force the current game to end
    /// </summary>
    public static readonly TranslationString Command_EndGame_Description = new("Command.EndGame.Description");

    /// <summary>
    /// Base Translation: Force a skip during the current meeting
    /// </summary>
    public static readonly TranslationString Command_ForceSkip_Description = new("Command.ForceSkip.Description");

    /// <summary>
    /// Base Translation: Kick a player from the game
    /// </summary>
    public static readonly TranslationString Command_Kick_Description = new("Command.Kick.Description");

    /// <summary>
    /// Base Translation: Show information about a player
    /// </summary>
    public static readonly TranslationString Command_PlayerInfo_Description = new("Command.PlayerInfo.Description");

    /// <summary>
    /// Base Translation: Show information about all players
    /// </summary>
    public static readonly TranslationString Command_PlayersInfo_Description = new("Command.PlayersInfo.Description");

    /// <summary>
    /// Base Translation: Set the command prefix
    /// </summary>
    public static readonly TranslationString Command_SetPrefix_Description = new("Command.SetPrefix.Description");

    /// <summary>
    /// Base Translation: Name the selected settings preset
    /// </summary>
    public static readonly TranslationString Command_PresetName_Description = new("Command.PresetName.Description");

    /// <summary>
    /// Base Translation: &lt;color=#ffffbe&gt;{0}&lt;/color&gt; is now named &lt;color=#e0b700&gt;{1}&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString Command_PresetName_Updated = new("Command.PresetName.Updated");

    /// <summary>
    /// Base Translation: &lt;color=#ffffbe&gt;{0}&lt;/color&gt; uses its default name again
    /// </summary>
    public static readonly TranslationString Command_PresetName_Cleared = new("Command.PresetName.Cleared");

    /// <summary>
    /// Base Translation: Command List
    /// </summary>
    public static readonly TranslationString Command_List_Title = new("Command.List.Title");

    /// <summary>
    /// Base Translation: Error:
    /// </summary>
    public static readonly TranslationString Command_Error_Title = new("Command.Error.Title");

    /// <summary>
    /// Base Translation: Invalid syntax!
    /// </summary>
    public static readonly TranslationString Command_Error_InvalidSyntax = new("Command.Error.InvalidSyntax");

    /// <summary>
    /// Base Translation: Player not found!
    /// </summary>
    public static readonly TranslationString Command_Error_PlayerNotFound = new("Command.Error.PlayerNotFound");

    /// <summary>
    /// Base Translation: BepInEx log file not found!
    /// </summary>
    public static readonly TranslationString Command_Error_LogNotFound = new("Command.Error.LogNotFound");

    /// <summary>
    /// Base Translation: Invalid command!
    /// </summary>
    public static readonly TranslationString Command_Error_InvalidCommand = new("Command.Error.InvalidCommand");

    /// <summary>
    /// Base Translation: This command can only be used in the lobby
    /// </summary>
    public static readonly TranslationString Command_Error_LobbyOnly = new("Command.Error.LobbyOnly");

    /// <summary>
    /// Base Translation: Only the host can use this command
    /// </summary>
    public static readonly TranslationString Command_Error_HostOnly = new("Command.Error.HostOnly");

    /// <summary>
    /// Base Translation: This command can only be used during gameplay
    /// </summary>
    public static readonly TranslationString Command_Error_GameplayOnly = new("Command.Error.GameplayOnly");

    /// <summary>
    /// Base Translation: Logs saved to &lt;color=#b1b1b1&gt;&apos;{0}&apos;&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString Command_Dump_Success = new("Command.Dump.Success");

    /// <summary>
    /// Base Translation: Diagnostic report saved as &lt;color=#b1b1b1&gt;&apos;{0}&apos;&lt;/color&gt;. Share this file when reporting a problem; it contains no lobby codes, friend codes or secrets.
    /// </summary>
    public static readonly TranslationString Command_Dump_ReportSaved = new("Command.Dump.ReportSaved");

    /// <summary>
    /// Base Translation: Warning
    /// </summary>
    public static readonly TranslationString Startup_Warning_Title = new("Startup.Warning.Title");

    /// <summary>
    /// Base Translation: {0} supports Among Us {1}.\\nAmong Us {2} is newer than the supported version, so you may run into bugs.
    /// </summary>
    public static readonly TranslationString Startup_GameVersion_Newer = new("Startup.GameVersion.Newer");

    /// <summary>
    /// Base Translation: {0} supports Among Us {1}.\\nAmong Us {2} is older than the supported version, so you may run into bugs.
    /// </summary>
    public static readonly TranslationString Startup_GameVersion_Older = new("Startup.GameVersion.Older");

    /// <summary>
    /// Base Translation: The old BetterAmongUs plugin is still installed:\\n{1}\\nRemove BetterAmongUs.dll from the BepInEx plugins folder so it does not load beside {0}. Your Better_Data folder is kept.
    /// </summary>
    public static readonly TranslationString Startup_LegacyPlugin_Found = new("Startup.LegacyPlugin.Found");

    /// <summary>
    /// Base Translation: The old BetterAmongUs plugin is loaded beside {0}. Both mods change the game and may conflict.\\nRemove BetterAmongUs.dll from the BepInEx plugins folder. Your Better_Data folder is kept.
    /// </summary>
    public static readonly TranslationString Startup_LegacyPlugin_Loaded = new("Startup.LegacyPlugin.Loaded");

    /// <summary>
    /// Base Translation: Command prefix changed from &lt;#c1c100&gt;{0}&lt;/color&gt; to &lt;#c1c100&gt;{1}&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString Command_Prefix_Updated = new("Command.Prefix.Updated");

    /// <summary>
    /// Base Translation: This command can only be used during a meeting
    /// </summary>
    public static readonly TranslationString Command_Error_MeetingOnly = new("Command.Error.MeetingOnly");

    /// <summary>
    /// Base Translation: Info
    /// </summary>
    public static readonly TranslationString Command_PlayerInfo_Info = new("Command.PlayerInfo.Info");

    /// <summary>
    /// Base Translation: ID
    /// </summary>
    public static readonly TranslationString Command_PlayerInfo_ID = new("Command.PlayerInfo.ID");

    /// <summary>
    /// Base Translation: Platform
    /// </summary>
    public static readonly TranslationString Command_PlayerInfo_Platform = new("Command.PlayerInfo.Platform");

    /// <summary>
    /// Base Translation: FriendCode
    /// </summary>
    public static readonly TranslationString Command_PlayerInfo_FriendCode = new("Command.PlayerInfo.FriendCode");

    /// <summary>
    /// Base Translation: Update
    /// </summary>
    public static readonly TranslationString Update_Button = new("Update.Button");

    /// <summary>
    /// Base Translation: {0} update installed!\nRestart the game to finish updating.
    /// </summary>
    public static readonly TranslationString Update_Complete = new("Update.Complete");

    /// <summary>
    /// Base Translation: Update failed: no internet connection.\nCheck your connection and press Update again.
    /// </summary>
    public static readonly TranslationString Update_Failed_NoInternet = new("Update.Failed.NoInternet");

    /// <summary>
    /// Base Translation: Update failed: the download link is missing or invalid.\nDownload the latest release from GitHub instead.
    /// </summary>
    public static readonly TranslationString Update_Failed_MissingLink = new("Update.Failed.MissingLink");

    /// <summary>
    /// Base Translation: Update failed: the download did not finish.\nYour current version is still installed. Press Update to try again.
    /// </summary>
    public static readonly TranslationString Update_Failed_Download = new("Update.Failed.Download");

    /// <summary>
    /// Base Translation: Update failed: the downloaded file is not a newer {0} build.\nYour current version is still installed. Download the latest release from GitHub instead.
    /// </summary>
    public static readonly TranslationString Update_Failed_InvalidPayload = new("Update.Failed.InvalidPayload");

    /// <summary>
    /// Base Translation: Update failed: the new file could not be installed.\nYour current version is still installed. Close other programs using the game folder and try again.
    /// </summary>
    public static readonly TranslationString Update_Failed_Install = new("Update.Failed.Install");

    /// <summary>
    /// Base Translation: Update failed unexpectedly.\nYour current version is still installed. Check the BepInEx log for details.
    /// </summary>
    public static readonly TranslationString Update_Failed_Unexpected = new("Update.Failed.Unexpected");

    /// <summary>
    /// Base Translation: Starting download{0}
    /// </summary>
    public static readonly TranslationString Update_Progress_Starting = new("Update.Progress.Starting");

    /// <summary>
    /// Base Translation: Downloading{0}
    /// </summary>
    public static readonly TranslationString Update_Progress_Downloading = new("Update.Progress.Downloading");

    /// <summary>
    /// Base Translation: Verifying update...
    /// </summary>
    public static readonly TranslationString Update_Progress_Verifying = new("Update.Progress.Verifying");

    /// <summary>
    /// Base Translation: Installing update...
    /// </summary>
    public static readonly TranslationString Update_Progress_Installing = new("Update.Progress.Installing");

    /// <summary>
    /// Base Translation: Download failed!
    /// </summary>
    public static readonly TranslationString Update_Progress_Failed = new("Update.Progress.Failed");
}