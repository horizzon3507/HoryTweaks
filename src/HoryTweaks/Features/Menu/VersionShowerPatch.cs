using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using HarmonyLib;

namespace HoryTweaks.Features.Menu;

[HarmonyPatch]
internal static class VersionShowerPatch
{
    [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
    [HarmonyPostfix]
    private static void VersionShower_Start_Postfix(VersionShower __instance)
    {
        string mark = TranslationStrings.BAUMark.LocalizedString;
        string bau = TranslationStrings.BAU.LocalizedString;
        __instance.text.text = $"<color=#ffffbe>{mark}{bau}{mark} {BAUPlugin.ModInfo.VERSION_STRING}</color> <color=#ababab>~</color> {Utils.GetPlatformName(BAUPlugin.PlatformData.Platform)} v{BAUPlugin.AmongUsVersion} ({BAUPlugin.AppVersion})";
    }
}
