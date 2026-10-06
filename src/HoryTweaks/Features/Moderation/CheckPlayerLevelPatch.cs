using BetterAmongUs.Generated;

using BetterAmongUs.Core.Moderation;
using BetterAmongUs.Utilities;
using HarmonyLib;
using BetterAmongUs.Features.GameOptions;
using BetterAmongUs.Game;

namespace BetterAmongUs.Features.Moderation;

[HarmonyPatch]
internal static class CheckPlayerLevelPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    private static void PlayerControl_FixedUpdate_Postfix(PlayerControl __instance)
    {
        if (__instance.Data == null)
            return;

        if (GameState.IsHost && GameState.IsLobby)
        {
            // Kick players below minimum level
            uint playerLevel = __instance.Data.PlayerLevel;
            int kickLevelBelow = BetterGameSettings.KickLevelBelow.GetInt();
            if (LevelKickRules.ShouldKick(playerLevel, kickLevelBelow, BetterGameSettings.KickLevelBelowMinimumPlayers.GetInt(), BAUPlugin.AllPlayerControls.Count, __instance.IsLocalPlayer(), BetterGameSettings.KickLevel.GetBool()))
            {
                __instance.TryKick(setReasonInfo: TranslationStrings.HostTools_LevelTooLow.Format(playerLevel, kickLevelBelow));
            }
        }
    }
}