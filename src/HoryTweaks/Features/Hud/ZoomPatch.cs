using HarmonyLib;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Utilities;
using UnityEngine;

namespace HoryTweaks.Features.Hud;

[HarmonyPatch]
internal class ZoomPatch
{
    private const float DefaultOrthoSize = 3.0f;
    private const float MaxOrthoSize = 18.0f;

    private static bool _wasZooming;
    private static float _lastOrthographicSize;
    private static Camera? _cachedMainCamera;

    private static Camera? GetMainCamera()
    {
        if (_cachedMainCamera == null || !_cachedMainCamera.isActiveAndEnabled)
        {
            _cachedMainCamera = Camera.main;
        }

        return _cachedMainCamera;
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    private static void HudManager_Update_Postfix(HudManager __instance)
    {
        var mainCamera = GetMainCamera();
        var localPlayer = PlayerControl.LocalPlayer;
        if (mainCamera == null || localPlayer == null)
            return;

        bool isAlive = localPlayer.IsAlive();
        bool canZoom = BAUConfigs.ScrollZoom.Value &&
            GameState.IsCanMove &&
            !localPlayer.IsGhostRole() &&
            (!GameState.IsInGamePlay || !isAlive);

        bool shouldReset = !canZoom && _wasZooming;
        float currentSize = mainCamera.orthographicSize;
        _lastOrthographicSize = currentSize;

        if (shouldReset)
        {
            SetZoomSize(mainCamera, reset: true);
            _wasZooming = false;
            currentSize = mainCamera.orthographicSize;
        }
        else if (canZoom)
        {
            float scrollY = Input.mouseScrollDelta.y;
            if (scrollY > 0f && currentSize > DefaultOrthoSize)
            {
                _wasZooming = true;
                SetZoomSize(mainCamera, zoomIn: true);
                currentSize = mainCamera.orthographicSize;
            }
            else if (scrollY < 0f &&
                (GameState.IsDead || GameState.IsFreePlay || GameState.IsLobby) &&
                currentSize < MaxOrthoSize)
            {
                _wasZooming = true;
                SetZoomSize(mainCamera, zoomOut: true);
                currentSize = mainCamera.orthographicSize;
            }
        }

        if (__instance.ShadowQuad != null)
        {
            bool showShadow = Mathf.Approximately(currentSize, DefaultOrthoSize) && isAlive;
            var shadowObj = __instance.ShadowQuad.gameObject;
            if (shadowObj.activeSelf != showShadow)
            {
                shadowObj.SetActive(showShadow);
            }
        }
    }

    private static void SetZoomSize(Camera mainCamera, bool zoomIn = false, bool zoomOut = false, bool reset = false)
    {
        if (!HudManager.InstanceExists)
            return;

        if (reset)
        {
            mainCamera.orthographicSize = DefaultOrthoSize;
            HudManager.Instance.UICamera.orthographicSize = DefaultOrthoSize;
            HudManager.Instance.Chat.transform.localScale = Vector3.one;

            if (GameState.IsMeeting && MeetingHud.Instance != null)
            {
                MeetingHud.Instance.transform.localScale = Vector3.one;
            }
        }
        else if (zoomIn || zoomOut)
        {
            float scale = zoomIn ? (1f / 1.5f) : 1.5f;
            mainCamera.orthographicSize *= scale;
            HudManager.Instance.UICamera.orthographicSize *= scale;
        }

        if (!Mathf.Approximately(mainCamera.orthographicSize, _lastOrthographicSize))
        {
            ResolutionManager.ResolutionChanged.Invoke(
                (float)Screen.width / Screen.height,
                Screen.width,
                Screen.height,
                Screen.fullScreen);
        }
    }
}
