using AmongUs.GameOptions;

using BetterAmongUs.Utilities;
using HarmonyLib;
using BetterAmongUs.Game.Players;

namespace BetterAmongUs.Game;

[HarmonyPatch]
internal static class RoleManagerPatch
{
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SetRole))]
    [HarmonyPrefix]
    private static void RoleManager_SetRole_Prefix(RoleManager __instance, PlayerControl targetPlayer, RoleTypes roleType)
    {
        // Store the original role when player dies (for ghost role purposes)
        if (roleType.IsGhostRole())
        {
            if (!targetPlayer.Data.RoleType.IsGhostRole())
            {
                targetPlayer.ExtendedData().RoleInfo.DeadDisplayRole = targetPlayer.Data.RoleType;
            }
        }
    }
}