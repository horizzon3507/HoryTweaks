using AmongUs.GameOptions;

using HoryTweaks.Generated;

using InnerNet;
using UnityEngine;
using HoryTweaks.Core.Moderation;
using HoryTweaks.Features.Moderation;
using HoryTweaks.Game;
using HoryTweaks.Game.Players;

namespace HoryTweaks.Utilities;

/// <summary>
/// Provides extension methods and utilities for working with PlayerControl instances.
/// </summary>
internal static partial class PlayerControlUtils
{

    /// <summary>
    /// Sets the outline effect on a player's character.
    /// </summary>
    /// <param name="player">The player to modify.</param>
    /// <param name="active">Whether to enable the outline.</param>
    /// <param name="color">The outline color (optional).</param>
    internal static void SetOutline(this PlayerControl player, bool active, Color? color = null)
    {
        player.cosmetics.currentBodySprite.BodySprite.material.SetFloat("_Outline", active ? 1 : 0);
        SpriteRenderer[] longModeParts = player.cosmetics.currentBodySprite.LongModeParts;
        for (int i = 0; i < longModeParts.Length; i++)
        {
            longModeParts[i].material.SetFloat("_Outline", active ? 1 : 0);
        }
        if (color != null)
        {
            player.cosmetics.currentBodySprite.BodySprite.material.SetColor("_OutlineColor", color.Value);
            longModeParts = player.cosmetics.currentBodySprite.LongModeParts;
            for (int i = 0; i < longModeParts.Length; i++)
            {
                longModeParts[i].material.SetColor("_OutlineColor", color.Value);
            }
        }
    }


    /// <summary>
    /// Sets the outline effect on a player's character using a hex color string.
    /// </summary>
    /// <param name="player">The player to modify.</param>
    /// <param name="active">Whether to enable the outline.</param>
    /// <param name="hexColor">The hex color string for the outline.</param>
    internal static void SetOutlineByHex(this PlayerControl player, bool active, string hexColor = "")
    {
        Color color = Utils.HexToColor32(hexColor);
        player.cosmetics.currentBodySprite.BodySprite.material.SetFloat("_Outline", active ? 1 : 0);
        SpriteRenderer[] longModeParts = player.cosmetics.currentBodySprite.LongModeParts;
        for (int i = 0; i < longModeParts.Length; i++)
        {
            longModeParts[i].material.SetFloat("_Outline", active ? 1 : 0);
        }

        player.cosmetics.currentBodySprite.BodySprite.material.SetColor("_OutlineColor", color);
        longModeParts = player.cosmetics.currentBodySprite.LongModeParts;
        for (int i = 0; i < longModeParts.Length; i++)
        {
            longModeParts[i].material.SetColor("_OutlineColor", color);
        }
    }


    /// <summary>
    /// Gets the role name of a player.
    /// </summary>
    /// <param name="player">The player to get the role name for.</param>
    /// <returns>The role name string.</returns>
    internal static string GetRoleName(this PlayerControl player)
    {
        if (player == null)
            return string.Empty;

        if (!player.IsAlive() && !player.IsGhostRole())
        {
            return player.ExtendedData().RoleInfo.DeadDisplayRole.GetRoleName();
        }

        if (player.Data != null)
        {
            return player.Data.RoleType.GetRoleName();
        }

        return string.Empty;
    }


    /// <summary>
    /// Gets the hex color code for a player's team.
    /// </summary>
    /// <param name="player">The player to get team color for.</param>
    /// <returns>The hex color string.</returns>
    internal static string GetTeamHexColor(this PlayerControl player) => player.Data.GetTeamHexColor();


    /// <summary>
    /// Gets the hex color code for a player data's team.
    /// </summary>
    /// <param name="data">The player data to get team color for.</param>
    /// <returns>The hex color string.</returns>
    internal static string GetTeamHexColor(this NetworkedPlayerInfo data)
    {
        if (data == null) return "#ffffff";

        if (data.IsImpostorTeam())
        {
            return Colors.ImpostorRed.ColorToHex();
        }
        else
        {
            return Colors.CrewmateBlue.ColorToHex();
        }
    }
}
