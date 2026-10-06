using BepInEx.Unity.IL2CPP.Utils;

using HarmonyLib;
using System.Collections;
using BetterAmongUs.Infrastructure.UnityInterop;

namespace BetterAmongUs.Game.Players;

[HarmonyPatch]
internal static class MonoExtensionPatch
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Awake))]
    [HarmonyPostfix]
    private static void PlayerControl_Awake_Postfix(PlayerControl __instance)
    {
        IMonoExtension.AddExtension<ExtendedPlayerControl>(__instance);
        __instance.StartCoroutine(CoAddExtensionPatch(__instance));
    }

    private static IEnumerator CoAddExtensionPatch(PlayerControl playerControl)
    {
        while (playerControl.Data == null)
        {
            yield return null;
        }

        IMonoExtension.AddExtension<ExtendedPlayerInfo>(playerControl.Data);
    }
}
