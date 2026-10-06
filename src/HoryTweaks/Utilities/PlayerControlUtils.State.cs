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
    /// Checks if a player is alive.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is alive.</returns>
    internal static bool IsAlive(this PlayerControl player)
    {
        if (player == null)
            return false;

        var data = player.Data;
        return data != null && !data.IsDead;
    }


    /// <summary>
    /// Checks if a player is in a vent.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is in a vent.</returns>
    internal static bool IsInVent(this PlayerControl player)
    {
        if (player == null)
            return false;

        if (player.inVent)
            return true;

        if (player.walkingToVent && !player.moveable)
            return true;

        var physics = player.MyPhysics;
        if (physics == null)
            return false;

        var animations = physics.Animations;
        if (animations == null)
            return false;

        return animations.IsPlayingEnterVentAnimation();
    }


    /// <summary>
    /// Checks if a player is currently shapeshifting.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is shapeshifting.</returns>
    internal static bool IsInShapeshift(this PlayerControl player) => player != null && (player.shapeshiftTargetPlayerId > -1 || player.shapeshifting) && !player.waitingForShapeshiftResponse;


    /// <summary>
    /// Checks if a player is in vanish mode as a Phantom.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is in vanish mode.</returns>
    internal static bool IsInVanish(this PlayerControl player)
    {
        if (player != null && player.Data.Role is PhantomRole phantomRole)
        {
            return phantomRole.fading;
        }
        return false;
    }


    /// <summary>
    /// Checks if a player has a specific role type.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <param name="role">The role type to check for.</param>
    /// <returns>True if the player has the specified role.</returns>
    internal static bool Is(this PlayerControl player, RoleTypes role)
    {
        if (player == null)
            return false;

        var data = player.Data;
        if (data == null)
            return false;

        return data.RoleType == role;
    }


    /// <summary>
    /// Checks if a player has a ghost role.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player has a ghost role.</returns>
    internal static bool IsGhostRole(this PlayerControl player)
    {
        if (player == null)
            return false;

        var data = player.Data;
        if (data == null)
            return false;

        return data.RoleType is RoleTypes.GuardianAngel or RoleTypes.SpiritGuide;
    }


    /// <summary>
    /// Checks if a player is on the impostor team.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is on the impostor team.</returns>
    internal static bool IsImpostorTeam(this PlayerControl player)
    {
        if (player == null)
            return false;

        var data = player.Data;
        if (data == null)
            return false;

        return data.IsImpostorTeam();
    }


    /// <summary>
    /// Checks if player data indicates the player is on the impostor team.
    /// </summary>
    /// <param name="data">The player data to check.</param>
    /// <returns>True if the player is on the impostor team.</returns>
    internal static bool IsImpostorTeam(this NetworkedPlayerInfo data)
    {
        if (data == null)
            return false;

        var roleBehaviour = data.RoleType.GetBehaviourPrefab();
        return roleBehaviour != null && roleBehaviour.IsImpostor;
    }


    /// <summary>
    /// Checks if a player is an impostor teammate of the local player.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is an impostor teammate.</returns>
    internal static bool IsImpostorTeammate(this PlayerControl player)
    {
        if (player == null)
            return false;

        var data = player.Data;
        if (data == null)
            return false;

        return data.IsImpostorTeammate();
    }


    /// <summary>
    /// Checks if a player data is an impostor teammate of the local player data.
    /// </summary>
    /// <param name="playerData">The player data to check.</param>
    /// <returns>True if the player data is an impostor teammate.</returns>
    internal static bool IsImpostorTeammate(this NetworkedPlayerInfo playerData)
    {
        if (playerData == null || PlayerControl.LocalPlayer == null)
            return false;

        bool localIsImpostor = PlayerControl.LocalPlayer.IsImpostorTeam();
        bool playerIsImpostor = playerData.IsImpostorTeam();

        return (playerData.IsLocalData() && localIsImpostor) ||
               (localIsImpostor && playerIsImpostor);
    }
}
