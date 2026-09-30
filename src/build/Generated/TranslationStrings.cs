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
    /// Base Translation: Better User
    /// </summary>
    public static readonly TranslationString Player_BetterUser = new("Player.BetterUser");

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
    /// Base Translation: Better Options
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
    /// Base Translation: Interface
    /// </summary>
    public static readonly TranslationString BetterOption_PageInterface = new("BetterOption.PageInterface");

    /// <summary>
    /// Base Translation: Gameplay
    /// </summary>
    public static readonly TranslationString BetterOption_PageGameplay = new("BetterOption.PageGameplay");

    /// <summary>
    /// Base Translation: Performance &amp; Files
    /// </summary>
    public static readonly TranslationString BetterOption_PagePerformance = new("BetterOption.PagePerformance");

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
    /// Base Translation: Better Settings
    /// </summary>
    public static readonly TranslationString BetterSetting = new("BetterSetting");

    /// <summary>
    /// Base Translation: Edit better settings for your lobby and gameplay.
    /// </summary>
    public static readonly TranslationString BetterSetting_Description = new("BetterSetting.Description");

    /// <summary>
    /// Base Translation: Set To:
    /// </summary>
    public static readonly TranslationString BetterSetting_SetTo = new("BetterSetting.SetTo");

    /// <summary>
    /// Base Translation: &lt;color=#07B400&gt;System Settings&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString BetterSetting_MainHeader_System = new("BetterSetting.MainHeader.System");

    /// <summary>
    /// Base Translation: &lt;color=#4f92ff&gt;Host tools&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString BetterSetting_MainHeader_HostTools = new("BetterSetting.MainHeader.HostTools");

    /// <summary>
    /// Base Translation: &lt;color=#d7d700&gt;Gameplay Settings&lt;/color&gt;
    /// </summary>
    public static readonly TranslationString BetterSetting_MainHeader_Gameplay = new("BetterSetting.MainHeader.Gameplay");

    /// <summary>
    /// Base Translation: &lt;color=#4f92ff&gt;Host Only&lt;/color&gt;
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
    /// Base Translation: Save the full log to the desktop
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
}