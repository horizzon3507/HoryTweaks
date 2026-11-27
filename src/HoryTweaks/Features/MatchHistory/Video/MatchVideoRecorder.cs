using System.Collections.Concurrent;
using HoryTweaks.Core.MatchHistory;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.Persistence;
using UnityEngine;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Manages real-POV MP4 match recording off Unity's main thread using a reusable frame pool and a hybrid
/// encoder (Windows Media Foundation on native Windows, with automatic FFmpeg fallback for Wine/Proton).
/// </summary>
internal static class MatchVideoRecorder
{
    private const string LogTag = "MatchVideo";
    internal const string TempSuffix = ".tmp.mp4";

    private static readonly object StateLock = new();
    private static readonly object PublishLock = new();
    private static RecordingSession? _activeSession;
    private static int _clearGeneration;
    private static bool _loggedMissingEncoder;

    internal static bool IsCapturing
    {
        get
        {
            var session = Volatile.Read(ref _activeSession);
            return session != null && session.IsCapturing;
        }
    }

    /// <summary>
    /// True once the background encoder thread has successfully initialized an encoder.
    /// Capture must not schedule GPU work before this point.
    /// </summary>
    internal static bool IsEncoderReady
    {
        get
        {
            var session = Volatile.Read(ref _activeSession);
            return session != null && session.IsEncoderReady;
        }
    }

    internal static int TargetWidth
    {
        get
        {
            var session = Volatile.Read(ref _activeSession);
            return session != null ? session.Width : 0;
        }
    }

    internal static int TargetHeight
    {
        get
        {
            var session = Volatile.Read(ref _activeSession);
            return session != null ? session.Height : 0;
        }
    }

    internal static void Begin(DateTime startedUtc)
    {
        Discard();

        if (BAUPlugin.ModInfo.Starlight || !BAUConfigs.MatchHistory.Value || !BAUConfigs.MatchVideoReplay.Value)
            return;

        try
        {
            var (width, height) = MatchVideoRules.ComputeScaledDimensions(Screen.width, Screen.height);
            string folder = BetterDataManager.Folders.matchHistoryFolderPath;
            Directory.CreateDirectory(folder);

            string finalName = MatchVideoRetention.FileNameFor(startedUtc);
            string finalPath = Path.Combine(folder, finalName);
            string tempPath = Path.Combine(folder, finalName[..^".mp4".Length] + TempSuffix);

            int generation = Volatile.Read(ref _clearGeneration);
            // AsyncGPUReadback reads raw texture memory: top-down rows on Direct3D/Vulkan (UV starts at
            // top) and bottom-up rows on OpenGL, so only OpenGL needs the vertical flip in conversion.
            var session = new RecordingSession(
                width,
                height,
                MatchVideoRules.TargetFps,
                MatchVideoRules.TargetBitrateBps,
                flipVertical: !SystemInfo.graphicsUVStartsAtTop,
                tempPath,
                finalPath,
                generation);

            lock (StateLock)
            {
                _activeSession = session;
            }

            session.Start();
            MatchVideoCaptureBehaviour.EnsureRunning();
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, LogTag);
            Discard();
        }
    }

    internal static byte[]? RentCaptureBuffer(out int width, out int height)
    {
        var session = Volatile.Read(ref _activeSession);
        if (session == null || !session.IsCapturing)
        {
            width = 0;
            height = 0;
            return null;
        }

        width = session.Width;
        height = session.Height;
        return session.TryRentBuffer();
    }

    internal static void SubmitCapturedFrame(byte[] rgbaBuffer, int elapsedMs)
    {
        var session = Volatile.Read(ref _activeSession);
        if (session == null || !session.TrySubmit(rgbaBuffer, elapsedMs))
        {
            session?.ReturnBuffer(rgbaBuffer);
        }
    }

    internal static void ReturnUnusedBuffer(byte[] rgbaBuffer)
    {
        var session = Volatile.Read(ref _activeSession);
        session?.ReturnBuffer(rgbaBuffer);
    }

    /// <summary>
    /// Stops capturing immediately and lets the background thread finalize the MP4 container and prune old files.
    /// </summary>
    internal static void Finish()
    {
        RecordingSession? session;
        lock (StateLock)
        {
            session = _activeSession;
            _activeSession = null;
        }

        session?.RequestStop(finalizeVideo: true);
    }

    /// <summary>
    /// Stops capturing and finalizes the MP4 only when enough footage exists to be worth watching
    /// (>= <see cref="MatchVideoRules.MinSaveableFrames"/> frames, or at least
    /// <see cref="MatchVideoRules.MinSaveableDurationMs"/> of footage); otherwise the temp file is deleted.
    /// </summary>
    internal static void FinishIfViable()
    {
        RecordingSession? session;
        lock (StateLock)
        {
            session = _activeSession;
            _activeSession = null;
        }

        session?.RequestStopViabilityGated();
    }

    /// <summary>
    /// Stops capture because continuing would hurt gameplay smoothness, keeping viable footage.
    /// </summary>
    internal static void StopForPerformance(string reason, bool keepViableVideo)
    {
        BAUPlugin.Logger.Warning($"Stopping POV video capture: {reason}.", LogTag);
        if (keepViableVideo)
        {
            FinishIfViable();
        }
        else
        {
            Discard();
        }
    }

    /// <summary>
    /// Stops capturing immediately and deletes the partial temp video file.
    /// </summary>
    internal static void Discard()
    {
        RecordingSession? session;
        lock (StateLock)
        {
            session = _activeSession;
            _activeSession = null;
        }

        session?.RequestStop(finalizeVideo: false);
    }

    /// <summary>
    /// Invalidates any finalizing or active video session so Delete All cannot be raced by a
    /// background encoder renaming a temp file after the directory snapshot was taken.
    /// </summary>
    internal static void InvalidateAndDiscardAll()
    {
        Interlocked.Increment(ref _clearGeneration);
        Discard();
    }

    internal static object PublicationSyncRoot => PublishLock;

    internal static void OnConfigToggled()
    {
        if (!BAUConfigs.MatchHistory.Value || !BAUConfigs.MatchVideoReplay.Value)
        {
            Discard();
        }
    }

    internal static void Shutdown()
    {
        Discard();
        MatchVideoCaptureBehaviour.DestroyIfPresent();
    }

    private static IMatchVideoEncoder? TryCreateEncoder(string tempPath, int width, int height, int fps, int bitrateBps)
    {
        bool isWine = NativePlatform.IsWineOrProton();
        string? mfError = null;
        if (OperatingSystem.IsWindows() && !isWine)
        {
            var mf = MediaFoundationMp4Encoder.TryCreate(tempPath, width, height, fps, bitrateBps, out mfError);
            if (mf != null)
                return mf;
        }

        // In-process Cisco OpenH264 + pure-C# MP4 muxer works on Linux/Proton, Steam Deck, Flatpak and Windows N.
        string? openH264Error = null;
        string? openH264Path = OpenH264Provisioner.EnsureAvailable();
        if (openH264Path != null)
        {
            var h264 = OpenH264Mp4Encoder.TryCreate(openH264Path, tempPath, width, height, fps, bitrateBps, out openH264Error);
            if (h264 != null)
                return h264;
        }

        string? ffmpegPath = FfmpegLocator.FindExecutable(
            BetterDataManager.Folders.fileFolderPath,
            Environment.GetEnvironmentVariable("PATH"),
            OperatingSystem.IsWindows(),
            isWine,
            File.Exists);

        string? ffmpegError = null;
        if (ffmpegPath != null)
        {
            var ff = FfmpegMp4Encoder.TryCreate(ffmpegPath, tempPath, width, height, fps, out ffmpegError);
            if (ff != null)
                return ff;
        }

        if (!_loggedMissingEncoder)
        {
            _loggedMissingEncoder = true;
            string mfPart = mfError != null ? $"MediaFoundation: {mfError}; " : string.Empty;
            string ohPart = openH264Path == null ? "OpenH264 unavailable; " : $"OpenH264: {openH264Error}; ";
            string ffPart = ffmpegPath == null ? "ffmpeg.exe not found" : $"ffmpeg: {ffmpegError}";
            BAUPlugin.Logger.Warning($"POV MP4 recording unavailable ({mfPart}{ohPart}{ffPart}). 2D match history still works normally.", LogTag);
        }

        return null;
    }

    private readonly record struct QueuedFrame(byte[] RgbaBuffer, int ElapsedMs);

    private sealed class RecordingSession
    {
        private readonly int _fps;
        private readonly int _bitrateBps;
        private readonly bool _flipVertical;
        private readonly string _tempPath;
        private readonly string _finalPath;
        private readonly int _generation;
        private readonly int _rgbaLength;
        private readonly ConcurrentBag<byte[]> _bufferPool = [];
        private readonly ConcurrentQueue<QueuedFrame> _pendingFrames = new();
        private readonly AutoResetEvent _signal = new(false);
        private readonly Thread _worker;

        private int _pendingCount;
        private int _capturing = 1;
        private int _stopRequested;
        private int _finalizeVideo;
        private int _viabilityGated;
        private int _encoderReady;
        private int _lastElapsedMs;

        internal int Width { get; }
        internal int Height { get; }
        internal bool IsCapturing => Volatile.Read(ref _capturing) != 0;
        internal bool IsEncoderReady => Volatile.Read(ref _encoderReady) != 0;

        internal RecordingSession(int width, int height, int fps, int bitrateBps, bool flipVertical, string tempPath, string finalPath, int generation)
        {
            Width = width;
            Height = height;
            _fps = fps;
            _bitrateBps = bitrateBps;
            _flipVertical = flipVertical;
            _tempPath = tempPath;
            _finalPath = finalPath;
            _generation = generation;
            _rgbaLength = width * height * 4;

            // Pre-allocate bounded pool so per-frame capture allocates zero managed heap memory.
            for (int i = 0; i < MatchVideoRules.FrameQueueCapacity + 1; i++)
            {
                _bufferPool.Add(new byte[_rgbaLength]);
            }

            _worker = new Thread(RunEncoderLoop)
            {
                Name = "HoryTweaks.VideoEncoder",
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal
            };
        }

        internal void Start() => _worker.Start();

        internal byte[]? TryRentBuffer()
        {
            if (!IsCapturing)
                return null;

            return _bufferPool.TryTake(out var buf) ? buf : null;
        }

        internal void ReturnBuffer(byte[] buffer)
        {
            if (buffer.Length == _rgbaLength && Volatile.Read(ref _stopRequested) == 0)
            {
                _bufferPool.Add(buffer);
            }
        }

        internal bool TrySubmit(byte[] rgbaBuffer, int elapsedMs)
        {
            if (!IsCapturing || Volatile.Read(ref _stopRequested) != 0)
                return false;

            if (elapsedMs > MatchVideoRules.MaxDurationMs)
            {
                Volatile.Write(ref _capturing, 0);
                return false;
            }

            if (Interlocked.Increment(ref _pendingCount) > MatchVideoRules.FrameQueueCapacity)
            {
                Interlocked.Decrement(ref _pendingCount);
                return false;
            }

            _pendingFrames.Enqueue(new QueuedFrame(rgbaBuffer, elapsedMs));
            try
            {
                _signal.Set();
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            return true;
        }

        internal void RequestStop(bool finalizeVideo)
        {
            Volatile.Write(ref _capturing, 0);
            if (finalizeVideo)
            {
                Interlocked.Exchange(ref _finalizeVideo, 1);
            }

            if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
            {
                try
                {
                    _signal.Set();
                }
                catch (ObjectDisposedException) { }
            }
        }

        internal void RequestStopViabilityGated()
        {
            Interlocked.Exchange(ref _viabilityGated, 1);
            RequestStop(finalizeVideo: true);
        }

        private void RunEncoderLoop()
        {
            IMatchVideoEncoder? encoder = null;
            int framesEncoded = 0;
            long frameDuration100ns = 10_000_000L / Math.Max(_fps, 1);
            long nextFallbackPts = 0;
            long lastPts = -1;

            try
            {
                encoder = TryCreateEncoder(_tempPath, Width, Height, _fps, _bitrateBps);
                if (encoder == null)
                {
                    Volatile.Write(ref _capturing, 0);
                    DrainAndDropPending();
                    return;
                }

                Volatile.Write(ref _encoderReady, 1);
                BAUPlugin.Logger.Log($"Started POV replay capture ({Width}x{Height}@{_fps}fps via {encoder.BackendName})", LogTag);
                byte[] yuv = new byte[PixelConverter.Nv12BufferSize(Width, Height)];
                bool useI420 = encoder.ExpectsI420;

                while (true)
                {
                    while (_pendingFrames.TryDequeue(out var frame))
                    {
                        Interlocked.Decrement(ref _pendingCount);
                        bool discardNow = Volatile.Read(ref _stopRequested) != 0 && Volatile.Read(ref _finalizeVideo) == 0;
                        if (discardNow)
                        {
                            continue;
                        }

                        if (useI420)
                        {
                            PixelConverter.Rgba32ToI420(frame.RgbaBuffer, yuv, Width, Height, _flipVertical);
                        }
                        else
                        {
                            PixelConverter.Rgba32ToNv12(frame.RgbaBuffer, yuv, Width, Height, _flipVertical);
                        }
                        ReturnBuffer(frame.RgbaBuffer);

                        long pts100ns = Math.Max((long)frame.ElapsedMs * 10_000L, nextFallbackPts);
                        if (pts100ns <= lastPts)
                        {
                            pts100ns = lastPts + frameDuration100ns;
                        }

                        long duration = frameDuration100ns;
                        if (!encoder.WriteYuvFrame(yuv, yuv.Length, pts100ns, duration))
                        {
                            BAUPlugin.Logger.Warning($"POV replay encoder ({encoder.BackendName}) stopped writing frames.", LogTag);
                            Volatile.Write(ref _capturing, 0);
                            encoder.Abort();
                            DrainAndDropPending();
                            return;
                        }

                        lastPts = pts100ns;
                        nextFallbackPts = pts100ns + frameDuration100ns;
                        framesEncoded++;
                        Volatile.Write(ref _lastElapsedMs, frame.ElapsedMs);
                    }

                    if (Volatile.Read(ref _stopRequested) != 0)
                        break;

                    _signal.WaitOne(100);
                }

                bool viable = framesEncoded >= MatchVideoRules.MinSaveableFrames
                    || (Volatile.Read(ref _lastElapsedMs) >= MatchVideoRules.MinSaveableDurationMs && framesEncoded > 0);
                bool shouldFinalize = Volatile.Read(ref _finalizeVideo) != 0
                    && framesEncoded > 0
                    && (Volatile.Read(ref _viabilityGated) == 0 || viable)
                    && Volatile.Read(ref _clearGeneration) == _generation;
                if (Volatile.Read(ref _viabilityGated) != 0 && !viable && framesEncoded > 0)
                {
                    BAUPlugin.Logger.Log($"Discarding partial POV video ({framesEncoded} frames below viability threshold).", LogTag);
                }
                if (shouldFinalize && encoder.Finish())
                {
                    bool published = false;
                    lock (PublishLock)
                    {
                        if (Volatile.Read(ref _clearGeneration) == _generation)
                        {
                            File.Move(_tempPath, _finalPath, overwrite: true);
                            MatchHistoryStore.PruneVideos();
                            published = true;
                        }
                        else
                        {
                            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
                        }
                    }

                    if (published)
                    {
                        long sizeKb = new FileInfo(_finalPath).Length / 1024;
                        BAUPlugin.Logger.Log($"Saved POV replay video ({framesEncoded} frames, {sizeKb} KB via {encoder.BackendName})", LogTag);
                    }
                }
                else
                {
                    encoder.Abort();
                }
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, LogTag);
                try { encoder?.Abort(); } catch { }
            }
            finally
            {
                Volatile.Write(ref _capturing, 0);
                DrainAndDropPending();
                try { encoder?.Dispose(); } catch { }
                _signal.Dispose();
            }
        }

        private void DrainAndDropPending()
        {
            while (_pendingFrames.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _pendingCount);
            }
        }
    }
}
