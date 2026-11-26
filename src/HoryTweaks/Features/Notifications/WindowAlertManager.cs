using System.Diagnostics;
using System.Runtime.InteropServices;
using HoryTweaks.Core.Notifications;
using HoryTweaks.Infrastructure.Configuration;
using UnityEngine;

namespace HoryTweaks.Features.Notifications;

/// <summary>
/// Alerts the player when Among Us is unfocused (Alt+Tab) on lobby full, match start or meeting start.
/// Uses Win32 <c>FlashWindowEx</c> (works on native Windows and Wine/Proton) plus a subtle in-game chime.
/// </summary>
internal static class WindowAlertManager
{
    private const uint FlashwAll = 0x00000003;
    private const uint FlashwTimerNoFg = 0x0000000C;

    private static readonly WindowAlertPolicy Policy = new();

    internal static void ResetLobby() => Policy.ResetLobby();

    internal static void CheckLobbyFull(int playerCount, int maxPlayers)
    {
        if (Policy.ObserveLobbyOccupancy(playerCount, maxPlayers))
        {
            TryAlert(WindowAlertReason.LobbyFull);
        }
    }

    internal static void NotifyGameStarted()
    {
        Policy.ResetLobby();
        TryAlert(WindowAlertReason.GameStarted);
    }

    internal static void NotifyMeetingStarted()
    {
        TryAlert(WindowAlertReason.MeetingStarted);
    }

    private static void TryAlert(WindowAlertReason _)
    {
        try
        {
            if (!Policy.ShouldAlert(BAUConfigs.WindowAlerts.Value, Application.isFocused, Time.realtimeSinceStartupAsDouble))
                return;

            TryFlashWindow();
            TryPlayChime();
        }
        catch
        {
            // Alerts are strictly best-effort and must never disrupt gameplay.
        }
    }

    private static void TryPlayChime()
    {
        if (!HudManager.InstanceExists || SoundManager.instance == null)
            return;

        var chat = HudManager.Instance.Chat;
        if (chat != null && chat.messageSound != null)
        {
            SoundManager.instance.PlaySound(chat.messageSound, false, 0.75f);
        }
    }

    private static void TryFlashWindow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        nint hwnd = FindGameWindowHandle();
        if (hwnd == 0)
            return;

        var info = new FlashWindowInfo
        {
            cbSize = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            hwnd = hwnd,
            dwFlags = FlashwAll | FlashwTimerNoFg,
            uCount = 5,
            dwTimeout = 0
        };

        FlashWindowEx(ref info);
    }

    private static nint FindGameWindowHandle()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            if (current.MainWindowHandle != IntPtr.Zero)
                return current.MainWindowHandle;

            uint currentPid = (uint)current.Id;
            nint found = 0;
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == currentPid)
                {
                    found = hwnd;
                    return false;
                }

                return true;
            }, 0);

            return found;
        }
        catch
        {
            return 0;
        }
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint cbSize;
        public nint hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo pwfi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
