using System.Runtime.InteropServices;

namespace HoryTweaks.Infrastructure.Logging;

/// <summary>
/// Platform helpers for configuring the Windows debug console without blocking Unity's main thread.
/// </summary>
internal static class ConsolePlatform
{
    private const int StdInputHandle = -10;
    private const uint EnableQuickEditMode = 0x0040;
    private const uint EnableExtendedFlags = 0x0080;

    /// <summary>
    /// Disables Windows Console QuickEdit mode so clicking inside the console window cannot pause
    /// subsequent console writes and stall the game. Safe no-op on non-Windows hosts or when no console exists.
    /// </summary>
    internal static bool TryDisableQuickEdit()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            nint handle = GetStdHandle(StdInputHandle);
            if (handle == 0 || handle == -1)
                return false;

            if (!GetConsoleMode(handle, out uint mode))
                return false;

            uint updated = (mode & ~EnableQuickEditMode) | EnableExtendedFlags;
            if (updated == mode)
                return true;

            return SetConsoleMode(handle, updated);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
