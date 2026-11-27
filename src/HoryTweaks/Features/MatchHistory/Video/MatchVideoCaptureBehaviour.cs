using System.Collections;
using System.Diagnostics;
using BepInEx.Unity.IL2CPP.Utils;
using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Plugin.Registration;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Hidden persistent MonoBehaviour that captures downscaled game frames at end-of-frame while a match
/// video is recording. Uses non-blocking AsyncGPUReadback with ping-pong render targets: the main thread
/// never waits on the GPU, so capture cannot stall gameplay the way synchronous ReadPixels did.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class MatchVideoCaptureBehaviour : MonoBehaviour
{
    private const string LogTag = "MatchVideo";
    private const int MaxInFlight = 2;

    private struct InFlightSlot
    {
        public bool Active;
        public int ElapsedMs;
        public NativeAsyncGpuReadbackRequest Request;
    }

    private static MatchVideoCaptureBehaviour? _instance;

    private readonly InFlightSlot[] _slots = new InFlightSlot[MaxInFlight];
    private readonly VideoCaptureScheduler _scheduler = new();
    private readonly Stopwatch _workWatch = new();

    private RenderTexture? _fullRt;
    private readonly RenderTexture?[] _scaledRts = new RenderTexture?[MaxInFlight];
    private int _fullWidth;
    private int _fullHeight;
    private int _scaledWidth;
    private int _scaledHeight;
    private float _captureStartRealtime;
    private bool _wasCapturing;
    private bool _loggedAvailability;
    private int _consecutiveReadbackErrors;

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

    private void Update()
    {
        if (!MatchVideoRecorder.IsCapturing)
        {
            if (_wasCapturing)
            {
                _wasCapturing = false;
                _loggedAvailability = false;
                _consecutiveReadbackErrors = 0;
                _scheduler.Reset();
                ReleaseResources();
            }

            return;
        }

        PollInFlightSlots();
    }

    [HideFromIl2Cpp]
    private void PollInFlightSlots()
    {
        double nowSec = Time.unscaledTimeAsDouble;
        float frameDelta = Time.unscaledDeltaTime;

        for (int i = 0; i < MaxInFlight; i++)
        {
            if (!_slots[i].Active)
                continue;

            _workWatch.Restart();
            var request = _slots[i].Request;
            NativeAsyncGpuReadback.Update(ref request);
            _slots[i].Request = request;

            if (!NativeAsyncGpuReadback.IsDone(ref request))
                continue;

            bool errored = NativeAsyncGpuReadback.HasError(ref request);
            if (errored)
            {
                _slots[i].Active = false;
                _consecutiveReadbackErrors++;
                _workWatch.Stop();
                FeedScheduler(_workWatch.Elapsed.TotalMilliseconds, frameDelta, nowSec);

                if (_consecutiveReadbackErrors >= 3)
                {
                    BAUPlugin.Logger.Warning("AsyncGPUReadback failed repeatedly; stopping POV video capture to protect game FPS.", LogTag);
                    MatchVideoRecorder.StopForPerformance("AsyncGPUReadback error", keepViableVideo: true);
                    ReleaseResources();
                    return;
                }

                continue;
            }

            byte[]? buffer = MatchVideoRecorder.RentCaptureBuffer(out int targetW, out int targetH);
            bool submitted = false;
            if (buffer != null && targetW > 0 && targetH > 0)
            {
                if (NativeAsyncGpuReadback.TryCopyRgbaData(ref request, targetW, targetH, buffer))
                {
                    MatchVideoRecorder.SubmitCapturedFrame(buffer, _slots[i].ElapsedMs);
                    submitted = true;
                }
            }

            if (!submitted && buffer != null)
            {
                MatchVideoRecorder.ReturnUnusedBuffer(buffer);
            }

            _slots[i].Active = false;
            _consecutiveReadbackErrors = 0;
            _workWatch.Stop();
            FeedScheduler(_workWatch.Elapsed.TotalMilliseconds, frameDelta, nowSec);
        }
    }

    [HideFromIl2Cpp]
    private void FeedScheduler(double workMs, float frameDelta, double nowSec)
    {
        var stopReason = _scheduler.RecordMainThreadWork(workMs, frameDelta, nowSec);
        if (stopReason == VideoCaptureStopReason.None)
            return;

        BAUPlugin.Logger.Warning(
            $"Stopping POV video capture to protect gameplay smoothness ({stopReason}, capture {MatchVideoRecorder.TargetWidth}x{MatchVideoRecorder.TargetHeight}). Viable footage is still saved.",
            LogTag);
        MatchVideoRecorder.StopForPerformance(stopReason.ToString(), keepViableVideo: true);
        ReleaseResources();
    }

    [HideFromIl2Cpp]
    private IEnumerator CaptureLoop()
    {
        var endOfFrame = new WaitForEndOfFrame();

        while (true)
        {
            if (!MatchVideoRecorder.IsCapturing || !MatchVideoRecorder.IsEncoderReady)
            {
                yield return null;
                continue;
            }

            if (!_wasCapturing)
            {
                _wasCapturing = true;
                _captureStartRealtime = Time.realtimeSinceStartup;
                _scheduler.Reset();
            }

            if (!_loggedAvailability && !NativeAsyncGpuReadback.IsAvailable)
            {
                _loggedAvailability = true;
                BAUPlugin.Logger.Warning("AsyncGPUReadback is unavailable on this GPU/driver; POV MP4 capture disabled to protect game FPS.", LogTag);
                MatchVideoRecorder.Discard();
                ReleaseResources();
                yield return null;
                continue;
            }

            int inFlightCount = 0;
            int freeSlot = -1;
            for (int i = 0; i < MaxInFlight; i++)
            {
                if (_slots[i].Active)
                {
                    inFlightCount++;
                }
                else if (freeSlot < 0)
                {
                    freeSlot = i;
                }
            }

            double nowSec = Time.unscaledTimeAsDouble;
            if (freeSlot < 0 || !_scheduler.ShouldStartCapture(nowSec, MatchVideoRecorder.IsEncoderReady, inFlightCount, MaxInFlight))
            {
                yield return null;
                continue;
            }

            yield return endOfFrame;

            if (!MatchVideoRecorder.IsCapturing || !MatchVideoRecorder.IsEncoderReady)
                continue;

            _workWatch.Restart();
            try
            {
                ScheduleCapture(freeSlot);
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, LogTag);
                MatchVideoRecorder.Discard();
                ReleaseResources();
            }

            _workWatch.Stop();
            if (MatchVideoRecorder.IsCapturing)
            {
                FeedScheduler(_workWatch.Elapsed.TotalMilliseconds, Time.unscaledDeltaTime, Time.unscaledTimeAsDouble);
            }
        }
    }

    [HideFromIl2Cpp]
    private void ScheduleCapture(int slot)
    {
        int screenW = Screen.width;
        int screenH = Screen.height;
        if (screenW <= 0 || screenH <= 0)
            return;

        int targetW = MatchVideoRecorder.TargetWidth;
        int targetH = MatchVideoRecorder.TargetHeight;
        if (targetW <= 0 || targetH <= 0)
            return;

        EnsureRenderTargets(screenW, screenH, targetW, targetH);
        var scaled = _scaledRts[slot];
        if (_fullRt == null || scaled == null)
            return;

        float captureNow = Time.realtimeSinceStartup;
        int elapsedMs = (int)Math.Min((captureNow - _captureStartRealtime) * 1000f, int.MaxValue);

        ScreenCapture.CaptureScreenshotIntoRenderTexture(_fullRt);
        Graphics.Blit(_fullRt, scaled);

        if (!NativeAsyncGpuReadback.TryRequestRgba32(scaled, out var request))
        {
            BAUPlugin.Logger.Warning("AsyncGPUReadback request failed; stopping POV video capture to protect game FPS.", LogTag);
            MatchVideoRecorder.StopForPerformance("AsyncGPUReadback request failed", keepViableVideo: true);
            ReleaseResources();
            return;
        }

        _slots[slot].Active = true;
        _slots[slot].ElapsedMs = elapsedMs;
        _slots[slot].Request = request;
        _scheduler.RecordCaptureScheduled(Time.unscaledTimeAsDouble);
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

        if (_scaledWidth != targetW || _scaledHeight != targetH)
        {
            for (int i = 0; i < MaxInFlight; i++)
            {
                if (_scaledRts[i] != null)
                {
                    _scaledRts[i]!.Release();
                    UnityEngine.Object.Destroy(_scaledRts[i]);
                    _scaledRts[i] = null;
                }
            }

            _scaledWidth = targetW;
            _scaledHeight = targetH;
        }

        for (int i = 0; i < MaxInFlight; i++)
        {
            if (_scaledRts[i] != null)
                continue;

            _scaledRts[i] = new RenderTexture(targetW, targetH, 0, GraphicsFormat.R8G8B8A8_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            _scaledRts[i]!.Create();
        }
    }

    [HideFromIl2Cpp]
    private void ReleaseResources()
    {
        // Never block on pending readbacks: in-flight requests die with their render targets.
        for (int i = 0; i < MaxInFlight; i++)
        {
            _slots[i].Active = false;
            _slots[i].Request = default;

            if (_scaledRts[i] != null)
            {
                _scaledRts[i]!.Release();
                UnityEngine.Object.Destroy(_scaledRts[i]);
                _scaledRts[i] = null;
            }
        }

        if (_fullRt != null)
        {
            _fullRt.Release();
            UnityEngine.Object.Destroy(_fullRt);
            _fullRt = null;
        }

        _fullWidth = 0;
        _fullHeight = 0;
        _scaledWidth = 0;
        _scaledHeight = 0;
    }
}
