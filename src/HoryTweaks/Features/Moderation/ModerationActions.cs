
using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using System.Diagnostics;
using HoryTweaks.Core.Moderation;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Persistence;

namespace HoryTweaks.Features.Moderation;

/// <summary>
/// Runs host moderation actions with the host and game-state checks applied in code, independent of the UI.
/// </summary>
internal static class ModerationActions
{
    /// <summary>
    /// Players the local host may act on: everyone in the current game except the local player.
    /// </summary>
    internal static List<PlayerControl> GetModeratablePlayers()
    {
        var players = new List<PlayerControl>();

        if (!GameState.IsInGame)
            return players;

        foreach (var player in BAUPlugin.AllPlayerControls)
        {
            if (player == null || player.Data == null || player.IsLocalPlayer())
                continue;

            players.Add(player);
        }

        players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return players;
    }

    internal static ModerationDenial Check(PlayerControl? target)
    {
        bool exists = target != null && target.Data != null;

        return ModerationRules.Evaluate(new ModerationContext(
            IsHost: GameState.IsHost,
            IsInGame: GameState.IsInGame,
            IsEnded: GameState.IsEnded,
            IsStarting: GameState.IsCountDown || GameState.IsInIntro,
            TargetExists: exists,
            TargetIsDummy: exists && target!.isDummy,
            TargetIsLocal: exists && target!.IsLocalPlayer(),
            TargetIsHost: exists && target!.IsHost(),
            TargetDataCollected: exists && target!.DataIsCollected()));
    }

    /// <summary>
    /// Kicks or bans the target through the existing cooldown-aware kick path. Returns false with an explanation when refused.
    /// </summary>
    internal static bool Execute(PlayerControl? target, ModerationActionKind kind, out string message)
    {
        var denial = Check(target);
        if (denial != ModerationDenial.None)
        {
            message = Describe(denial);
            return false;
        }

        bool ban = kind == ModerationActionKind.Ban;
        target!.Kick(ban, TranslationStrings.ModerationCenter_ActionReason.LocalizedString);

        message = ban
            ? TranslationStrings.ModerationCenter_Banned.Format(target.GetPlayerNameAndColor())
            : TranslationStrings.ModerationCenter_Kicked.Format(target.GetPlayerNameAndColor());
        return true;
    }

    internal static string Describe(ModerationDenial denial) => denial switch
    {
        ModerationDenial.NotHost => TranslationStrings.ModerationCenter_NotHost.LocalizedString,
        ModerationDenial.NotInGame => TranslationStrings.ModerationCenter_NotInGame.LocalizedString,
        ModerationDenial.GameStarting => TranslationStrings.ModerationCenter_GameStarting.LocalizedString,
        ModerationDenial.GameEnded => TranslationStrings.ModerationCenter_GameEnded.LocalizedString,
        ModerationDenial.TargetMissing => TranslationStrings.ModerationCenter_TargetMissing.LocalizedString,
        ModerationDenial.TargetIsDummy => TranslationStrings.ModerationCenter_TargetIsDummy.LocalizedString,
        ModerationDenial.TargetIsSelf => TranslationStrings.ModerationCenter_TargetIsSelf.LocalizedString,
        ModerationDenial.TargetIsHost => TranslationStrings.ModerationCenter_TargetIsHost.LocalizedString,
        ModerationDenial.TargetNotReady => TranslationStrings.ModerationCenter_TargetNotReady.LocalizedString,
        _ => string.Empty
    };

    internal static string GetListPath(ModerationListKind kind) => kind switch
    {
        ModerationListKind.Player => BetterDataManager.Files.banPlayerListFilePath,
        ModerationListKind.Name => BetterDataManager.Files.banNameListFilePath,
        _ => BetterDataManager.Files.banChatListFilePath
    };

    /// <summary>
    /// Explains a ban list and opens it in the system text editor when not in gameplay.
    /// Editing happens in the external editor because the files are re-read on every check, so no in-game editor is needed.
    /// </summary>
    internal static string OpenList(ModerationListKind kind)
    {
        string path = GetListPath(kind);
        string fileName = Path.GetFileName(path);

        if (!File.Exists(path))
            return TranslationStrings.ModerationCenter_ListMissing.Format(fileName);

        int count;
        try
        {
            count = ModerationLists.CountEntries(File.ReadAllLines(path));
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Log($"Failed to read {fileName}: {ex.Message}", "ModerationCenter");
            count = 0;
        }

        var info = kind switch
        {
            ModerationListKind.Player => TranslationStrings.ModerationCenter_PlayerListInfo,
            ModerationListKind.Name => TranslationStrings.ModerationCenter_NameListInfo,
            _ => TranslationStrings.ModerationCenter_ChatListInfo
        };
        string summary = info.Format(fileName, count.ToString());

        if (BAUPlugin.ModInfo.Starlight || (GameState.IsInGame && !GameState.IsLobby))
            return summary + "\n" + TranslationStrings.ModerationCenter_ListOpenBlocked.LocalizedString;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Log($"Failed to open {fileName}: {ex.Message}", "ModerationCenter");
            return summary + "\n" + TranslationStrings.ModerationCenter_ListOpenBlocked.LocalizedString;
        }

        return summary + "\n" + TranslationStrings.ModerationCenter_ListOpened.LocalizedString;
    }
}
