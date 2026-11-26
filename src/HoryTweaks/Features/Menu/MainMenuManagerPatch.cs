using HarmonyLib;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Remote.Updates;
using HoryTweaks.Utilities;
using UnityEngine;

namespace HoryTweaks.Features.Menu;

[HarmonyPatch]
internal static class MainMenuManagerPatch
{
    private const float RecolorIntervalSeconds = 0.25f;

    internal static PassiveButton? ButtonPrefab;

    private static nint _cachedManagerPtr;
    private static PassiveButton?[]? _cachedButtons;
    private static float _nextRecolorTime;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
    [HarmonyPostfix]
    private static void MainMenuManager_LateUpdate_Postfix(MainMenuManager __instance)
    {
        if (__instance == null)
            return;

        float now = Time.unscaledTime;
        if (now < _nextRecolorTime)
            return;

        _nextRecolorTime = now + RecolorIntervalSeconds;
        ApplyButtonColors(__instance);
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    [HarmonyPostfix]
    private static void MainMenuManager_Start_Postfix(MainMenuManager __instance)
    {
        if (__instance == null)
            return;

        _cachedManagerPtr = __instance.Pointer;
        _cachedButtons = CollectButtons(__instance);
        _nextRecolorTime = 0f;

        var background = __instance.transform.Find("MainUI/AspectScaler/BackgroundTexture");
        if (background != null)
        {
            background.gameObject.SetSpriteColors(sprite => GameObjectUtils.AddColor(sprite));
        }

        ApplyButtonColors(__instance);

        if (ButtonPrefab == null)
        {
            ButtonPrefab = UnityEngine.Object.Instantiate(__instance.inventoryButton);
            ButtonPrefab.gameObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(ButtonPrefab);
        }

        BAUUpdateManager.Instance?.OnMainMenu();
    }

    [HarmonyPatch(typeof(EjectMainMenu), nameof(EjectMainMenu.Start))]
    [HarmonyPrefix]
    private static bool EjectMainMenu_Start_Prefix(EjectMainMenu __instance)
    {
        __instance.gameObject.SetActive(false);
        return false;
    }

    private static PassiveButton?[] CollectButtons(MainMenuManager manager) =>
    [
        manager.playButton,
        manager.inventoryButton,
        manager.shopButton,
        manager.playLocalButton,
        manager.PlayOnlineButton,
        manager.backButtonOnline,
        manager.newsButton,
        manager.myAccountButton,
        manager.settingsButton,
        manager.howToPlayButton,
        manager.freePlayButton,
        manager.accountCTAButton,
        manager.accountStatsButton
    ];

    private static void ApplyButtonColors(MainMenuManager manager)
    {
        if (_cachedButtons == null || _cachedManagerPtr != manager.Pointer)
        {
            _cachedManagerPtr = manager.Pointer;
            _cachedButtons = CollectButtons(manager);
        }

        for (int i = 0; i < _cachedButtons.Length; i++)
        {
            var button = _cachedButtons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            button.gameObject.SetUIColors(static sprite => sprite.color == Color.white, "Icon", "Background");
        }
    }
}
