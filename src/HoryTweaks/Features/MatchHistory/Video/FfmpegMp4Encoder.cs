using System.Diagnostics;
using HoryTweaks.Core.MatchHistory;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Streaming H.264 MP4 encoder backed by an optional external <c>ffmpeg</c> process receiving raw NV12 frames on stdin.
/// Used on Wine/Proton and as a fallback on Windows editions without Media Foundation.
/// </summary>
internal sealed class FfmpegMp4Encoder : IMatchVideoEncoder
{
    private const int FinalizeTimeoutMs = 15_000;

    private readonly string _tempPath;
    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly FfmpegFramePacer _pacer;
    private byte[]? _previousFrame;
    private bool _closed;
    private bool _disposed;

    public string BackendName => "FFmpeg";
    public bool ExpectsI420 => false;

    private FfmpegMp4Encoder(string tempPath, Process process, int fps)
    {
        _tempPath = tempPath;
        _process = process;
        _stdin = process.StandardInput.BaseStream;
        _pacer = new FfmpegFramePacer(10_000_000L / Math.Max(fps, 1));
    }

    internal static FfmpegMp4Encoder? TryCreate(string ffmpegPath, string tempMp4Path, int width, int height, int fps, out string? error)
    {
        error = null;
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = FfmpegLocator.BuildArguments(width, height, fps, tempMp4Path),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false
            };

            process = Process.Start(startInfo);
            if (process == null)
            {
                error = "Failed to start ffmpeg process";
                return null;
            }

            // Drain stderr asynchronously so ffmpeg never blocks on a full pipe buffer.
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();

            if (process.HasExited)
            {
                error = $"ffmpeg exited immediately with code {process.ExitCode}";
                process.Dispose();
                TryDeleteTemp(tempMp4Path);
                return null;
            }

            return new FfmpegMp4Encoder(tempMp4Path, process, fps);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try { process?.Dispose(); } catch { }
            TryDeleteTemp(tempMp4Path);
            return null;
        }
    }

    public bool WriteYuvFrame(byte[] yuvBuffer, int length, long timestamp100ns, long duration100ns)
    {
        if (_disposed || _closed || _process.HasExited)
            return false;

        try
        {
            // Raw pipes carry no timestamps: repeat the previous frame across capture gaps so a
            // constant-framerate encode keeps 1x playback speed when capture FPS steps down.
            int duplicates = _pacer.ComputeDuplicatesBeforeNextFrame(timestamp100ns);
            if (_previousFrame == null || _previousFrame.Length < length)
            {
                _previousFrame = new byte[length];
            }

            for (int i = 0; i < duplicates; i++)
            {
                _stdin.Write(_previousFrame, 0, length);
            }

            _stdin.Write(yuvBuffer, 0, length);
            Buffer.BlockCopy(yuvBuffer, 0, _previousFrame, 0, length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Finish()
    {
        if (_disposed || _closed)
            return false;

        _closed = true;
        try
        {
            _stdin.Flush();
            _stdin.Close();
        }
        catch { }

        try
        {
            if (!_process.WaitForExit(FinalizeTimeoutMs))
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
                TryDeleteTemp(_tempPath);
                return false;
            }

            bool ok = _process.ExitCode == 0 && File.Exists(_tempPath) && new FileInfo(_tempPath).Length > 0;
            if (!ok)
            {
                TryDeleteTemp(_tempPath);
            }

            return ok;
        }
        catch
        {
            TryDeleteTemp(_tempPath);
            return false;
        }
    }

    public void Abort()
    {
        _closed = true;
        try { _stdin.Close(); } catch { }
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch { }
        TryDeleteTemp(_tempPath);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_closed)
        {
            Abort();
        }

        try { _process.Dispose(); } catch { }
    }

    private static void TryDeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
