using System.Globalization;

namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Resolves an optional <c>ffmpeg</c> executable for fallback H.264 MP4 encoding and formats the encoder command line.
/// On Windows and Wine/Proton only native Windows <c>ffmpeg.exe</c> binaries are selected, because Wine does not
/// reliably deliver Win32 stdin pipe EOF to native Linux ELF executables (Linux/Proton uses in-process OpenH264 instead).
/// </summary>
internal static class FfmpegLocator
{
    internal static string? FindExecutable(
        string? betterDataFolder,
        string? pathEnvironment,
        bool isWindows,
        bool isWineOrProton,
        Func<string, bool> fileExists)
    {
        if (fileExists == null)
            throw new ArgumentNullException(nameof(fileExists));

        string binaryName = isWindows ? "ffmpeg.exe" : "ffmpeg";

        if (!string.IsNullOrWhiteSpace(betterDataFolder))
        {
            string[] localCandidates =
            [
                Path.Combine(betterDataFolder, "Tools", binaryName),
                Path.Combine(betterDataFolder, binaryName)
            ];

            foreach (string candidate in localCandidates)
            {
                if (fileExists(candidate))
                    return candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(pathEnvironment))
        {
            char separator = isWindows ? ';' : ':';
            foreach (string rawDir in pathEnvironment.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                string dir = rawDir.Trim().Trim('"');
                if (dir.Length == 0)
                    continue;

                string candidate = Path.Combine(dir, binaryName);
                if (fileExists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    internal static string BuildArguments(int width, int height, int fps, string tempOutputPath)
    {
        if (width <= 0 || height <= 0 || fps <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Video dimensions and FPS must be positive.");
        if (string.IsNullOrWhiteSpace(tempOutputPath))
            throw new ArgumentException("Output path is required.", nameof(tempOutputPath));

        string escapedOutput = tempOutputPath.Replace("\"", "\\\"");
        return string.Create(CultureInfo.InvariantCulture,
            $"-y -hide_banner -loglevel error -f rawvideo -pixel_format nv12 -video_size {width}x{height} -framerate {fps} -i pipe:0 -c:v libx264 -preset ultrafast -tune zerolatency -crf 28 -maxrate 900k -bufsize 1800k -pix_fmt yuv420p -movflags +faststart -f mp4 \"{escapedOutput}\"");
    }
}
