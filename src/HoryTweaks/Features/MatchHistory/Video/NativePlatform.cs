using System.Runtime.InteropServices;

namespace HoryTweaks.Features.MatchHistory.Video;

/// <summary>
/// Runtime environment probes used to pick the safest native video encoder and window alert path.
/// </summary>
internal static class NativePlatform
{
    private static int _wineState = -1;

    /// <summary>
    /// Returns true when the Windows process is running under Wine or Steam Proton.
    /// </summary>
    internal static bool IsWineOrProton()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        int cached = Volatile.Read(ref _wineState);
        if (cached >= 0)
            return cached == 1;

        bool detected = false;
        try
        {
            nint ntdll = GetModuleHandleW("ntdll.dll");
            if (ntdll != 0)
            {
                detected = GetProcAddress(ntdll, "wine_get_version") != 0;
            }
        }
        catch
        {
            detected = false;
        }

        Volatile.Write(ref _wineState, detected ? 1 : 0);
        return detected;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern nint GetProcAddress(nint hModule, string procName);
}
