using BepInEx.Unity.IL2CPP.Utils;
using HoryTweaks.Generated;
using HoryTweaks.Game;
using HoryTweaks.Features.Chat;
using HoryTweaks.Infrastructure.UnityInterop;
using InnerNet;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HoryTweaks.Utilities;

/// <summary>
/// Provides utility methods for string manipulation, network operations, player lookups, and game utilities.
/// </summary>
internal static partial class Utils
{
    /// <summary>
    /// Gets ClientData from a client ID.
    /// </summary>
    /// <param name="clientId">The client ID to look up.</param>
    /// <returns>The ClientData if found, null otherwise.</returns>
    internal static ClientData? ClientFromClientId(ClientId clientId) =>
        AmongUsClient.Instance.allClients.FirstOrDefaultIl2Cpp(cd => cd.Id == clientId);


    /// <summary>
    /// Gets NetworkedPlayerInfo from a player ID.
    /// </summary>
    /// <param name="playerId">The player ID to look up.</param>
    /// <returns>The NetworkedPlayerInfo if found, null otherwise.</returns>
    internal static NetworkedPlayerInfo? PlayerDataFromPlayerId(PlayerId playerId) =>
        GameData.Instance.AllPlayers.FirstOrDefaultIl2Cpp(data => data.PlayerId == playerId);


    /// <summary>
    /// Gets NetworkedPlayerInfo from a client ID.
    /// </summary>
    /// <param name="clientId">The client ID to look up.</param>
    /// <returns>The NetworkedPlayerInfo if found, null otherwise.</returns>
    internal static NetworkedPlayerInfo? PlayerDataFromClientId(ClientId clientId) =>
        GameData.Instance.AllPlayers.FirstOrDefaultIl2Cpp(data => data.ClientId == clientId);


    /// <summary>
    /// Gets NetworkedPlayerInfo from a friend code.
    /// </summary>
    /// <param name="friendCode">The friend code to look up.</param>
    /// <returns>The NetworkedPlayerInfo if found, null otherwise.</returns>
    internal static NetworkedPlayerInfo? PlayerDataFromFriendCode(string friendCode) =>
        GameData.Instance.AllPlayers.FirstOrDefaultIl2Cpp(data => data.FriendCode == friendCode);


    /// <summary>
    /// Gets PlayerControl from a player ID.
    /// </summary>
    /// <param name="playerId">The player ID to look up.</param>
    /// <returns>The PlayerControl if found, null otherwise.</returns>
    internal static PlayerControl? PlayerFromPlayerId(PlayerId playerId) =>
        BAUPlugin.AllPlayerControls.FirstOrDefault(player => player.PlayerId == playerId);


    /// <summary>
    /// Gets PlayerControl from a client ID.
    /// </summary>
    /// <param name="clientId">The client ID to look up.</param>
    /// <returns>The PlayerControl if found, null otherwise.</returns>
    internal static PlayerControl? PlayerFromClientId(ClientId clientId) =>
        BAUPlugin.AllPlayerControls.FirstOrDefault(player => player.GetClientId() == clientId);


    /// <summary>
    /// Gets PlayerControl from a network ID.
    /// </summary>
    /// <param name="netId">The network ID to look up.</param>
    /// <returns>The PlayerControl if found, null otherwise.</returns>
    internal static PlayerControl? PlayerFromNetId(NetId netId) =>
        BAUPlugin.AllPlayerControls.FirstOrDefault(player => player.NetId == netId);

    // Chat functionality

    /// <summary>
    /// Gets the hashed PUID of a player.
    /// </summary>
    /// <param name="player">The player to get the hashed PUID for.</param>
    /// <returns>The hashed PUID string.</returns>
    internal static string GetHashPuid(PlayerControl player)
    {
        if (player == null)
            return "";

        if (player.Data == null)
            return "";

        if (player.Data.Puid == null)
            return "";

        return GetHashStr(player.Data.Puid);
    }

    /// <summary>
    /// Gets the platform name for a player.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <param name="useTag">Whether to include the platform tag (PC, Console, Mobile).</param>
    /// <returns>The platform name string.</returns>
    internal static string GetPlatformName(PlayerControl player, bool useTag = false)
    {
        if (player == null)
            return string.Empty;

        var client = player.GetClient();
        if (client == null)
            return string.Empty;

        if (client.PlatformData == null)
            return string.Empty;

        return GetPlatformName(client.PlatformData.Platform, useTag);
    }


    /// <summary>
    /// Gets the platform name from a Platforms enum value.
    /// </summary>
    /// <param name="platform">The platform enum value.</param>
    /// <param name="useTag">Whether to include the platform tag.</param>
    /// <returns>The platform name string.</returns>
    internal static string GetPlatformName(Platforms platform, bool useTag = false)
    {
        var (platformName, tag) = platform switch
        {
            Platforms.StandaloneSteamPC => ("Steam", "PC"),
            Platforms.StandaloneEpicPC => ("Epic Games", "PC"),
            Platforms.StandaloneWin10 => ("Microsoft Store", "PC"),
            Platforms.StandaloneMac => ("Mac OS", "PC"),
            Platforms.StandaloneItch => ("Itch.io", "PC"),
            Platforms.Xbox => ("Xbox", "Console"),
            Platforms.Playstation => ("Playstation", "Console"),
            Platforms.Switch => ("Switch", "Console"),
            Platforms.Android => ("Android", "Mobile"),
            Platforms.IPhone => ("IPhone", "Mobile"),
            (Platforms)CustomPlatforms.Starlight => ("Starlight", "Mobile"),
            Platforms.Unknown => ("None", ""),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrEmpty(platformName))
            return string.Empty;

        return useTag && !string.IsNullOrEmpty(tag) ? $"{tag}: {platformName}" : platformName;
    }
}
