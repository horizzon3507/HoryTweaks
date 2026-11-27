using System.Diagnostics;
using HoryTweaks.Core.Platform;
using HoryTweaks.Features.MatchHistory.Video;
using UnityEngine;

namespace HoryTweaks.Infrastructure.Platform;

/// <summary>
/// Opens files and folders in the desktop shell across native Windows and Wine/Proton. Never throws:
/// on total failure the path is copied to the clipboard so the player can still navigate to it.
/// On Wine/Proton the first candidate is winebrowser with a file URI, which reaches the Linux
/// desktop's native xdg-open and avoids the Win32Exception (6) "Invalid handle" crash entirely.
/// </summary>
internal static class ShellOpener
{
    private const string LogTag = "Shell";

    /// <summary>
    /// Attempts to open <paramref name="path"/> with the desktop shell, trying platform-appropriate
    /// fallback candidates in order. Returns false (and copies the path to the clipboard) when every
    /// candidate fails to launch.
    /// </summary>
    internal static bool TryOpen(string path, bool isDirectory)
    {
        Exception? lastError = null;
        var attempts = ShellOpenPolicy.BuildLaunchAttempts(path, isDirectory, OperatingSystem.IsWindows(), NativePlatform.IsWineOrProton());

        foreach (var attempt in attempts)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = attempt.FileName,
                    Arguments = attempt.Arguments,
                    UseShellExecute = attempt.UseShellExecute
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (lastError != null)
        {
            BAUPlugin.Logger.Error(lastError, LogTag);
        }

        try
        {
            GUIUtility.systemCopyBuffer = ShellOpenPolicy.NormalizeWindowsPath(path);
        }
        catch { }

        return false;
    }
}
