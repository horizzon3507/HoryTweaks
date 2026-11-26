using HarmonyLib;
using HoryTweaks.Core.Moderation;
using HoryTweaks.Features.GameOptions;
using HoryTweaks.Game;
using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using UnityEngine;

namespace HoryTweaks.Features.Moderation;

[HarmonyPatch]
internal static class CheckPlayerLevelPatch
{
    private const float CheckIntervalSeconds = 0.5f;
    private static float _nextCheckTime;

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    private static void PlayerControl_FixedUpdate_Postfix(PlayerControl __instance)
    {
        if (!GameState.IsHost || !GameState.IsLobby || !BetterGameSettings.KickLevel.GetBool())
            return;

        if (__instance == null || __instance.Data == null || __instance.IsLocalPlayer())
            return;

        float now = Time.unscaledTime;
        if (now < _nextCheckTime)
            return;

        _nextCheckTime = now + CheckIntervalSeconds;

        int kickLevelBelow = BetterGameSettings.KickLevelBelow.GetInt();
        int minimumPlayers = BetterGameSettings.KickLevelBelowMinimumPlayers.GetInt();
        var players = BAUPlugin.AllPlayerControls;
        int playerCount = players.Count;

        for (int i = 0; i < playerCount; i++)
        {
            var player = players[i];
            if (player == null || player.Data == null || player.IsLocalPlayer())
                continue;

            uint playerLevel = player.Data.PlayerLevel;
            if (LevelKickRules.ShouldKick(playerLevel, kickLevelBelow, minimumPlayers, playerCount, false, true))
            {
                player.TryKick(setReasonInfo: TranslationStrings.HostTools_LevelTooLow.Format(playerLevel, kickLevelBelow));
            }
        }
    }
}
