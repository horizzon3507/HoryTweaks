using HarmonyLib;
using HoryTweaks.Core.Chat;
using HoryTweaks.Utilities;
using InnerNet;

namespace HoryTweaks.Features.Chat;

/// <summary>
/// Client-side session chat mute list. Hides chat bubbles and chat popups from muted players locally
/// without affecting any other player in the lobby.
/// </summary>
[HarmonyPatch]
internal static class LocalMuteManager
{
    private static readonly LocalMuteList Muted = new();

    internal static int Count => Muted.Count;

    internal static bool IsMuted(PlayerControl? player)
    {
        if (player == null || player.IsLocalPlayer())
            return false;

        return Muted.IsMuted(player.PlayerId);
    }

    internal static bool Toggle(PlayerControl player)
    {
        string? name = player.Data != null ? player.Data.PlayerName : null;
        return Muted.Toggle(player.PlayerId, name);
    }

    internal static bool Unmute(PlayerControl player) => Muted.Unmute(player.PlayerId);

    internal static int ClearAll() => Muted.Clear();

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    [HarmonyPrefix]
    private static bool ChatController_AddChat_Prefix(PlayerControl sourcePlayer)
    {
        return !IsMuted(sourcePlayer);
    }

    [HarmonyPatch(typeof(ChatNotification), nameof(ChatNotification.SetUp))]
    [HarmonyPrefix]
    private static bool ChatNotification_SetUp_Prefix(PlayerControl sender)
    {
        return !IsMuted(sender);
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnPlayerLeft_Postfix(ClientData data)
    {
        if (data != null && data.Character != null)
        {
            Muted.Unmute(data.Character.PlayerId);
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    [HarmonyPostfix]
    private static void AmongUsClient_ExitGame_Postfix() => Muted.Clear();

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    [HarmonyPostfix]
    private static void InnerNetClient_DisconnectInternal_Postfix() => Muted.Clear();
}
