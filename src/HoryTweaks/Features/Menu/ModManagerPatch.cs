using HarmonyLib;
using HoryTweaks.Features.Hud;
using HoryTweaks.Modules.Support;
using HoryTweaks.Remote;
using HoryTweaks.Utilities;
using UnityEngine;

namespace HoryTweaks.Features.Menu;

[HarmonyPatch]
internal static class ModManagerPatch
{
    private static readonly Vector3 DownloadIconOffset = new(-0.7f, 0f, 1f);

    private static SpriteRenderer? _modStamp;
    private static SpriteRenderer? _downloadIcon;
    private static Sprite? _customModStampSprite;
    private static nint _shownStampManagerPtr;
    private static Vector3 _lastModStampPosition;
    private static bool _hasLastModStampPosition;

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
    [HarmonyPostfix]
    private static void LateUpdate_Postfix(ModManager __instance)
    {
        if (__instance == null)
            return;

        if (_downloadIcon == null)
        {
            _downloadIcon = UnityEngine.Object.Instantiate(__instance.ModStamp, __instance.transform);
            _downloadIcon.sprite = Utils.LoadSprite("HoryTweaks.Resources.Images.Icons.Downloading.png", 250);
            _downloadIcon.name = "DownloadIcon";
            _downloadIcon.enabled = false;
            _hasLastModStampPosition = false;
        }
        else
        {
            bool downloading = GithubAPI.Downloading;
            if (_downloadIcon.enabled != downloading)
            {
                _downloadIcon.enabled = downloading;
            }

            if (downloading)
            {
                Vector3 stampPos = __instance.ModStamp.transform.localPosition;
                if (!_hasLastModStampPosition || stampPos != _lastModStampPosition)
                {
                    _downloadIcon.transform.localPosition = stampPos + DownloadIconOffset;
                    _lastModStampPosition = stampPos;
                    _hasLastModStampPosition = true;
                }

                float alpha = Mathf.PingPong(Time.unscaledTime * 0.5f, 0.5f) + 0.25f;
                Color current = _downloadIcon.color;
                _downloadIcon.color = new Color(current.r, current.g, current.b, alpha);
            }
        }

        if (SplashIntroPatch.IsReallyDoneLoading && _shownStampManagerPtr != __instance.Pointer)
        {
            __instance.ShowModStamp();
            _shownStampManagerPtr = __instance.Pointer;
        }

        if (__instance.ModStamp != null && __instance.ModStamp.gameObject.activeSelf)
        {
            if (_modStamp == null)
            {
                _modStamp = __instance.ModStamp.GetComponent<SpriteRenderer>();
            }

            if (_modStamp != null && !BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_CustomModStamp))
            {
                if (_customModStampSprite == null)
                {
                    _customModStampSprite = Utils.LoadSprite("HoryTweaks.Resources.Images.HoryTweaks-Mod.png", 1450f);
                }

                if (_customModStampSprite != null && _modStamp.sprite != _customModStampSprite)
                {
                    _modStamp.sprite = _customModStampSprite;
                }
            }
        }

        BetterNotificationManager.Update();
    }
}
