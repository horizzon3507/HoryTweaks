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
    /// Gets the ClientData associated with a player.
    /// </summary>
    /// <param name="player">The player to get client data for.</param>
    /// <returns>The ClientData if found, null otherwise.</returns>
    internal static ClientData? GetClient(this PlayerControl player)
    {
        if (AmongUsClient.Instance == null || player == null)
            return null;

        try
        {
            foreach (var client in AmongUsClient.Instance.allClients)
            {
                if (client == null) continue;
                if (client.Character == null) continue;

                if (client.Character.PlayerId == player.PlayerId)
                    return client;
            }
        }
        catch (InvalidOperationException)
        {
            var snapshot = AmongUsClient.Instance.allClients.ToArray();
            foreach (var client in snapshot)
            {
                if (client == null) continue;
                if (client.Character == null) continue;

                if (client.Character.PlayerId == player.PlayerId)
                    return client;
            }
        }

        return null;
    }


    /// <summary>
    /// Gets the client ID of a player.
    /// </summary>
    /// <param name="player">The player to get the client ID for.</param>
    /// <returns>The client ID, or -1 if not found.</returns>
    internal static int GetClientId(this PlayerControl player)
    {
        if (player == null)
            return -1;

        var client = player.GetClient();
        if (client == null)
            return -1;

        return client.Id;
    }


    /// <summary>
    /// Gets the player's name with color formatting based on their outfit color.
    /// </summary>
    /// <param name="player">The player to get the name for.</param>
    /// <returns>The colored player name string.</returns>
    internal static string GetPlayerNameAndColor(this PlayerControl player)
    {
        if (player == null)
            return string.Empty;

        if (player.Data == null)
            return string.Empty;

        try
        {
            return $"<color={Utils.Color32ToHex(Palette.PlayerColors[player.Data.DefaultOutfit.ColorId])}>{player.Data.PlayerName}</color>";
        }
        catch
        {
            return player.Data.PlayerName;
        }
    }


    /// <summary>
    /// Checks if a player's character data has been fully loaded and received from the host.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if player data is complete, false otherwise.</returns>
    internal static bool DataIsCollected(this PlayerControl player)
    {
        if (player == null) return false;

        if (player.isDummy || GameState.IsLocalGame)
        {
            return true;
        }

        if (player.cosmetics == null)
            return false;

        if (player.cosmetics.nameText == null)
            return false;

        string loading = TranslationStrings.Player_Loading.LocalizedString;
        string nameText = player.cosmetics.nameText.text;

        if (nameText == "???" || nameText == "Player" || nameText == loading ||
            string.IsNullOrEmpty(nameText) ||
            player.Data == null ||
            player.CurrentOutfit == null ||
            player.CurrentOutfit.ColorId == -1)
        {
            return false;
        }

        return true;
    }


    /// <summary>
    /// Checks if a player is the local player.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>True if the player is the local player.</returns>
    internal static bool IsLocalPlayer(this PlayerControl player) => player != null && PlayerControl.LocalPlayer != null && player == PlayerControl.LocalPlayer;


    /// <summary>
    /// Checks if a player data is the local player data.
    /// </summary>
    /// <param name="playerData">The player data to check.</param>
    /// <returns>True if the player data is the local player data.</returns>
    internal static bool IsLocalData(this NetworkedPlayerInfo playerData) => playerData != null && PlayerControl.LocalPlayer != null && playerData == PlayerControl.LocalPlayer.Data;


    /// <summary>
    /// Gets the ID of the vent the player is currently in.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>The vent ID, or -1 if not in a vent.</returns>
    internal static int GetPlayerVentId(this PlayerControl player)
    {
        if (player == null)
            return -1;

        if (!(ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var systemType) &&
              systemType.TryCast<VentilationSystem>() is VentilationSystem ventilationSystem))
            return 0;

        return ventilationSystem.PlayersInsideVents.TryGetValue(player.PlayerId, out var playerIdVentId) ? playerIdVentId : -1;
    }


    /// <summary>
    /// Gets the custom position of a player.
    /// </summary>
    /// <param name="player">The player to get the position for.</param>
    /// <returns>The player's position as a Vector2.</returns>
    internal static Vector2 GetCustomPosition(this PlayerControl player) =>
        player.transform.position;


    /// <summary>
    /// Gets the hashed PUID of a player.
    /// </summary>
    /// <param name="player">The player to get the hashed PUID for.</param>
    /// <returns>The hashed PUID string.</returns>
    internal static string GetHashPuid(this PlayerControl player)
    {
        return player.Data.GetHashPuid() ?? "";
    }


    /// <summary>
    /// Gets the hashed PUID from player data.
    /// </summary>
    /// <param name="data">The player data to get the hashed PUID for.</param>
    /// <returns>The hashed PUID string.</returns>
    internal static string GetHashPuid(this NetworkedPlayerInfo data)
    {
        if (data == null)
            return string.Empty;

        if (data.Puid == null)
            return string.Empty;

        return Utils.GetHashStr(data.Puid);
    }


    /// <summary>
    /// Gets the hashed friend code of a player.
    /// </summary>
    /// <param name="player">The player to get the hashed friend code for.</param>
    /// <returns>The hashed friend code string.</returns>
    internal static string GetHashFriendcode(this PlayerControl player)
    {
        return player.Data.GetHashFriendcode() ?? "";
    }


    /// <summary>
    /// Gets the hashed friend code from player data.
    /// </summary>
    /// <param name="data">The player data to get the hashed friend code for.</param>
    /// <returns>The hashed friend code string.</returns>
    internal static string GetHashFriendcode(this NetworkedPlayerInfo data)
    {
        if (data == null)
            return string.Empty;

        if (data.FriendCode == null)
            return string.Empty;

        return Utils.GetHashStr(data.FriendCode);
    }
}
