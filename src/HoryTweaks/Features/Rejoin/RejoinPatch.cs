
using HarmonyLib;
using InnerNet;

namespace BetterAmongUs.Features.Rejoin;

[HarmonyPatch]
internal static class RejoinPatch
{
    // Track the lobby the local player joined and stop any running rejoin loop.
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnGameJoined_Postfix()
    {
        AutoRejoinManager.NotifyGameJoined();
    }

    // Every disconnect funnels through here before the error screen is shown.
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    [HarmonyPostfix]
    private static void InnerNetClient_DisconnectInternal_Postfix(DisconnectReasons reason)
    {
        AutoRejoinManager.NotifyDisconnected(reason);
    }

    // Cancel the countdown when the user dismisses the disconnect popup.
    [HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.Close))]
    [HarmonyPostfix]
    private static void DisconnectPopup_Close_Postfix()
    {
        AutoRejoinManager.NotifyPopupClosed();
    }

    [HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.RegainUIControl))]
    [HarmonyPostfix]
    private static void DisconnectPopup_RegainUIControl_Postfix()
    {
        AutoRejoinManager.NotifyPopupClosed();
    }
}
