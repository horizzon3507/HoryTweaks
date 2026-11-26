using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Plugin.Registration;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Hidden persistent MonoBehaviour that captures downscaled game frames at end-of-frame while a match video is recording.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class MatchVideoCaptureBehaviour : MonoBehaviour
{
    private const string LogTag = "MatchVideo";

    private static MatchVideoCaptureBehaviour? _instance;

    private RenderTexture? _fullRt;
    private RenderTexture? _scaledRt;
    private Texture2D? _readbackTex;
    private int _fullWidth;
    private int _fullHeight;
    private int _scaledWidth;
    private int _scaledHeight;
    private float _captureStartRealtime;
    private float _nextCaptureTime;
    private bool _wasCapturing;

    internal static void EnsureRunning()
    {
        if (_instance != null)
            return;

        var go = new GameObject("HoryTweaks.MatchVideoCapture")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        UnityEngine.Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<MatchVideoCaptureBehaviour>();
    }

    internal static void DestroyIfPresent()
    {
        if (_instance != null)
        {
            var obj = _instance.gameObject;
            _instance = null;
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
    }

    private void Start()
    {
        this.StartCoroutine(CaptureLoop());
    }

    private void OnDestroy()
    {
        ReleaseResources();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator CaptureLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();
        float interval = 1f / MatchVideoRules.TargetFps;

        while (true)
        {
            if (!MatchVideoRecorder.IsCapturing)
            {
                if (_wasCapturing)
                {
                    _wasCapturing = false;
                    ReleaseResources();
                }

                yield return null;
                continue;
            }

            float now = Time.realtimeSinceStartup;
            if (!_wasCapturing)
            {
                _wasCapturing = true;
                _captureStartRealtime = now;
                _nextCaptureTime = now;
            }

            if (now < _nextCaptureTime)
            {
                yield return null;
                continue;
            }

            yield return endOfFrame;

            if (!MatchVideoRecorder.IsCapturing)
                continue;

            float captureNow = Time.realtimeSinceStartup;
            _nextCaptureTime = Math.Max(_nextCaptureTime + interval, captureNow + interval * 0.5f);

            try
            {
                CaptureOneFrame((int)Math.Min((captureNow - _captureStartRealtime) * 1000f, int.MaxValue));
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, LogTag);
                MatchVideoRecorder.Discard();
                ReleaseResources();
            }
        }
    }

    [HideFromIl2Cpp]
    private void CaptureOneFrame(int elapsedMs)
    {
        int screenW = Screen.width;
        int screenH = Screen.height;
        if (screenW <= 0 || screenH <= 0)
            return;

        byte[]? buffer = MatchVideoRecorder.RentCaptureBuffer(out int targetW, out int targetH);
        if (buffer == null || targetW <= 0 || targetH <= 0)
            return;

        bool submitted = false;
        try
        {
            EnsureRenderTargets(screenW, screenH, targetW, targetH);
            if (_fullRt == null || _scaledRt == null || _readbackTex == null)
                return;

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_fullRt);
            Graphics.Blit(_fullRt, _scaledRt);

            RenderTexture? previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _scaledRt;
                _readbackTex.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            var raw = _readbackTex.GetRawTextureData();
            if (raw == null || raw.Length < buffer.Length)
                return;

            raw.CopyTo(buffer, 0);
            MatchVideoRecorder.SubmitCapturedFrame(buffer, elapsedMs);
            submitted = true;
        }
        finally
        {
            if (!submitted)
            {
                MatchVideoRecorder.ReturnUnusedBuffer(buffer);
            }
        }
    }

    [HideFromIl2Cpp]
    private void EnsureRenderTargets(int screenW, int screenH, int targetW, int targetH)
    {
        if (_fullRt == null || _fullWidth != screenW || _fullHeight != screenH)
        {
            if (_fullRt != null)
            {
                _fullRt.Release();
                UnityEngine.Object.Destroy(_fullRt);
            }

            _fullRt = new RenderTexture(screenW, screenH, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            _fullRt.Create();
            _fullWidth = screenW;
            _fullHeight = screenH;
        }

        if (_scaledRt == null || _readbackTex == null || _scaledWidth != targetW || _scaledHeight != targetH)
        {
            if (_scaledRt != null)
            {
                _scaledRt.Release();
                UnityEngine.Object.Destroy(_scaledRt);
            }

            if (_readbackTex != null)
            {
                UnityEngine.Object.Destroy(_readbackTex);
            }

            _scaledRt = new RenderTexture(targetW, targetH, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            _scaledRt.Create();

            _readbackTex = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _scaledWidth = targetW;
            _scaledHeight = targetH;
        }
    }

    [HideFromIl2Cpp]
    private void ReleaseResources()
    {
        if (_fullRt != null)
        {
            _fullRt.Release();
            UnityEngine.Object.Destroy(_fullRt);
            _fullRt = null;
        }

        if (_scaledRt != null)
        {
            _scaledRt.Release();
            UnityEngine.Object.Destroy(_scaledRt);
            _scaledRt = null;
        }

        if (_readbackTex != null)
        {
            UnityEngine.Object.Destroy(_readbackTex);
            _readbackTex = null;
        }

        _fullWidth = 0;
        _fullHeight = 0;
        _scaledWidth = 0;
        _scaledHeight = 0;
    }
}
