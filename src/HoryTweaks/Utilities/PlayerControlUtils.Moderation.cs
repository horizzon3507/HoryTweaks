using AmongUs.GameOptions;

using BetterAmongUs.Generated;

using InnerNet;
using UnityEngine;
using BetterAmongUs.Core.Moderation;
using BetterAmongUs.Features.Moderation;
using BetterAmongUs.Game;
using BetterAmongUs.Game.Players;

namespace BetterAmongUs.Utilities;

/// <summary>
/// Provides extension methods and utilities for working with PlayerControl instances.
/// </summary>
internal static partial class PlayerControlUtils
{

    /// <summary>
    /// Kicks a player from the game as soon as it is safe to do so.
    /// </summary>
    /// <param name="player">The player to kick.</param>
    /// <param name="ban">Whether to ban the player.</param>
    /// <param name="setReasonInfo">Custom reason message for the kick.</param>
    /// <param name="bypassDataCheck">Whether to bypass the data collection check.</param>
    /// <param name="forceBan">Whether to force a ban regardless of settings.</param>
    internal static void Kick(this PlayerControl player, bool ban = false, string setReasonInfo = "", bool bypassDataCheck = false, bool forceBan = false)
    {
        if (!player.CanKick(ban, bypassDataCheck, forceBan, out var shouldBan)) return;
        KickCooldownManager.ScheduleAction(() => { player.PerformKick(shouldBan, setReasonInfo); });
    }


    /// <summary>
    /// Kicks a player from the game immediately if it is safe to do so, otherwise does nothing.
    /// This should be used instead of <see cref="Kick"/> in places where this method is repeatedly called, like Update methods for example.
    /// </summary>
    /// <param name="player">The player to kick.</param>
    /// <param name="ban">Whether to ban the player.</param>
    /// <param name="setReasonInfo">Custom reason message for the kick.</param>
    /// <param name="bypassDataCheck">Whether to bypass the data collection check.</param>
    /// <param name="forceBan">Whether to force a ban regardless of settings.</param>
    internal static void TryKick(this PlayerControl player, bool ban = false, string setReasonInfo = "", bool bypassDataCheck = false, bool forceBan = false)
    {
        if (!KickCooldownManager.IsReady()) return;
        if (!player.CanKick(ban, bypassDataCheck, forceBan, out var shouldBan)) return;
        KickCooldownManager.Trigger();
        player.PerformKick(shouldBan, setReasonInfo);
    }


    /// <summary>
    /// Kicks a player from the game immediately, regardless of cooldown.
    /// Only use this if the target player is extremely dangerous and must be removed immediately.
    /// </summary>
    /// <param name="player">The player to kick.</param>
    /// <param name="ban">Whether to ban the player.</param>
    /// <param name="setReasonInfo">Custom reason message for the kick.</param>
    /// <param name="bypassDataCheck">Whether to bypass the data collection check.</param>
    /// <param name="forceBan">Whether to force a ban regardless of settings.</param>
    internal static void KickImmediate(this PlayerControl player, bool ban = false, string setReasonInfo = "", bool bypassDataCheck = false, bool forceBan = false)
    {
        if (!player.CanKick(ban, bypassDataCheck, forceBan, out var shouldBan)) return;
        player.PerformKick(shouldBan, setReasonInfo);
    }


    /// <summary>
    /// Determines whether the specified player can be kicked from the game and sets the outcome.
    /// </summary>
    /// <param name="player">The player to evaluate.</param>
    /// <param name="ban">Whether to ban the player.</param>
    /// <param name="bypassDataCheck">Whether to bypass the data collection check.</param>
    /// <param name="forceBan">Whether to force a ban regardless of settings.</param>
    /// <param name="shouldBan">
    /// When the method returns, this value indicates whether the player should be banned
    /// based on the provided flags and internal checks.
    /// </param>
    /// <returns><c>true</c> if the player can be kicked/banned, <c>false</c> otherwise.</returns>
    private static bool CanKick(this PlayerControl player, bool ban, bool bypassDataCheck, bool forceBan, out bool shouldBan)
    {
        return HostPlayerChecks.CanKick(
            GameState.IsHost,
            player.IsLocalPlayer(),
            player.DataIsCollected(),
            bypassDataCheck,
            player.IsHost(),
            player.isDummy,
            ban,
            forceBan,
            out shouldBan);
    }


    /// <summary>
    /// Kicks a player from the game with an optional ban.
    /// </summary>
    /// <param name="player">The player to kick.</param>
    /// <param name="ban">Whether to ban the player.</param>
    /// <param name="setReasonInfo">Custom reason message for the kick.</param>
    private static void PerformKick(this PlayerControl player, bool ban = false, string setReasonInfo = "")
    {
        string reasonText = string.Empty;
        if (setReasonInfo != "")
        {
            reasonText = string.Format(setReasonInfo, ban ? TranslationStrings.HostTools_Ban.LocalizedString.ToLower() : TranslationStrings.HostTools_Kick.LocalizedString.ToLower());
            PlayerJoinAndLeftPatch.BetterShowNotification(player.Data, forceReasonText: reasonText);
        }

        ModerationHistory.SetPendingReason(reasonText);
        AmongUsClient.Instance.KickPlayer(player.GetClientId(), ban);
    }


    /// <summary>
    /// Checks if a player is the game host.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is the host.</returns>
    internal static bool IsHost(this PlayerControl player)
    {
        if (player == null)
            return false;

        if (player.Data == null)
            return false;

        if (GameData.Instance == null)
            return false;

        return GameData.Instance.GetHost() == player.Data;
    }


    /// <summary>
    /// Reports a player with a specified reason.
    /// </summary>
    /// <param name="player">The player to report.</param>
    /// <param name="reason">The reason for the report.</param>
    internal static void ReportPlayer(this PlayerControl player, ReportReasons reason = ReportReasons.None)
    {
        if (player == null)
            return;

        var client = player.GetClient();
        if (client == null)
            return;

        if (client.HasBeenReported)
        {
            AmongUsClient.Instance.ReportPlayer(player.GetClientId(), reason);
        }
    }
}
