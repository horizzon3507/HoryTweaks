using System.Net.Http;
using System.Security.Cryptography;
using HoryTweaks.Core.Compression;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.Persistence;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Locates and (when needed, e.g. on Linux/Proton or Windows N) downloads Cisco's official
/// <c>openh264-2.6.0-win64.dll</c> with SHA-256 verification on both archive and extracted binary.
/// </summary>
internal static class OpenH264Provisioner
{
    private const string LogTag = "MatchVideo";
    internal const string DllFileName = "openh264-2.6.0-win64.dll";
    internal const string DownloadUrl = "https://ciscobinary.openh264.org/openh264-2.6.0-win64.dll.bz2";
    internal const string ExpectedBz2Sha256 = "dab5f2a872777f9a58b69bfa9fbcf20d9f82f2d6ec91383fd70bff49bd34ac9f";
    internal const string ExpectedDllSha256 = "2076cb5675ec6c1a4c70e7a2a322552f547b6eeed649d6dfcd9e02a543b24691";

    private static readonly object ProvisionLock = new();
    private static int _backgroundStarted;
    private static bool _downloadFailedThisSession;

    internal static string ToolsFolder => Path.Combine(BetterDataManager.Folders.fileFolderPath, "Tools");
    internal static string DefaultDllPath => Path.Combine(ToolsFolder, DllFileName);

    /// <summary>
    /// Returns an installed OpenH264 Win64 DLL path if already present in <c>Better_Data/Tools/</c>.
    /// </summary>
    internal static string? FindInstalledDll()
    {
        try
        {
            if (File.Exists(DefaultDllPath))
                return DefaultDllPath;

            if (Directory.Exists(ToolsFolder))
            {
                foreach (string file in Directory.GetFiles(ToolsFolder, "openh264*.dll"))
                {
                    return file;
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Starts a one-time background download of Cisco OpenH264 when running on Linux/Proton so POV video
    /// recording is ready before the first match begins.
    /// </summary>
    internal static void PrewarmIfNeeded()
    {
        if (BAUPlugin.ModInfo.Starlight || !BAUConfigs.MatchHistory.Value || !BAUConfigs.MatchVideoReplay.Value)
            return;

        if (!NativePlatform.IsWineOrProton())
            return;

        if (FindInstalledDll() != null)
            return;

        if (Interlocked.Exchange(ref _backgroundStarted, 1) != 0)
            return;

        var worker = new Thread(() =>
        {
            try
            {
                EnsureAvailable();
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Warning($"OpenH264 background setup failed: {ex.Message}", LogTag);
            }
        })
        {
            Name = "HoryTweaks.OpenH264Setup",
            IsBackground = true,
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        worker.Start();
    }

    /// <summary>
    /// Returns the path to <c>openh264-2.6.0-win64.dll</c>, downloading and SHA-256 verifying it once if missing.
    /// Must only be called on a background thread.
    /// </summary>
    internal static string? EnsureAvailable()
    {
        string? existing = FindInstalledDll();
        if (existing != null)
            return existing;

        lock (ProvisionLock)
        {
            existing = FindInstalledDll();
            if (existing != null)
                return existing;

            if (_downloadFailedThisSession)
                return null;

            try
            {
                Directory.CreateDirectory(ToolsFolder);
                BAUPlugin.Logger.Log($"Downloading Cisco OpenH264 ({DllFileName}) for in-process MP4 replay encoding...", LogTag);

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"HoryTweaks/{BAUPlugin.ModInfo.VERSION}");

                byte[] bz2Bytes = client.GetByteArrayAsync(DownloadUrl).GetAwaiter().GetResult();
                string bz2Hash = ComputeSha256Hex(bz2Bytes);
                if (!bz2Hash.Equals(ExpectedBz2Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _downloadFailedThisSession = true;
                    BAUPlugin.Logger.Error($"OpenH264 archive SHA-256 mismatch (got {bz2Hash}).", LogTag);
                    return null;
                }

                byte[] dllBytes = BZip2Decoder.Decompress(bz2Bytes, maxOutputBytes: 4 * 1024 * 1024);
                string dllHash = ComputeSha256Hex(dllBytes);
                if (!dllHash.Equals(ExpectedDllSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _downloadFailedThisSession = true;
                    BAUPlugin.Logger.Error($"OpenH264 DLL SHA-256 mismatch (got {dllHash}).", LogTag);
                    return null;
                }

                string tempPath = DefaultDllPath + ".tmp";
                File.WriteAllBytes(tempPath, dllBytes);
                File.Move(tempPath, DefaultDllPath, overwrite: true);

                BAUPlugin.Logger.Log($"Installed verified Cisco OpenH264 codec to {DefaultDllPath}", LogTag);
                return DefaultDllPath;
            }
            catch (Exception ex)
            {
                _downloadFailedThisSession = true;
                BAUPlugin.Logger.Warning($"Could not download Cisco OpenH264: {ex.Message}", LogTag);
                return null;
            }
        }
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
