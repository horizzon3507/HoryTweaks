using HoryTweaks.Core.Platform;
using Xunit;

namespace HoryTweaks.Tests.Platform;

public class ShellOpenPolicyTests
{
    [Fact]
    public void NormalizeWindowsPath_converts_forward_slashes()
    {
        Assert.Equal(
            @"C:\users\steamuser\AppData\Among Us\BetterDataV2.json",
            ShellOpenPolicy.NormalizeWindowsPath("C:/users/steamuser/AppData/Among Us\\BetterDataV2.json"));
    }

    [Fact]
    public void NormalizeWindowsPath_collapses_duplicate_slashes_but_preserves_unc_prefix()
    {
        Assert.Equal(@"C:\foo\bar", ShellOpenPolicy.NormalizeWindowsPath(@"C:\foo\\bar"));
        Assert.Equal(@"\\server\share\file", ShellOpenPolicy.NormalizeWindowsPath(@"\\server\share\\file"));
    }

    [Fact]
    public void NormalizeWindowsPath_trims_trailing_slashes_but_keeps_drive_root()
    {
        Assert.Equal(@"C:\foo\bar", ShellOpenPolicy.NormalizeWindowsPath(@"C:\foo\bar\"));
        Assert.Equal(@"C:\", ShellOpenPolicy.NormalizeWindowsPath(@"C:\"));
        Assert.Equal(@"C:\", ShellOpenPolicy.NormalizeWindowsPath("C:"));
    }

    [Fact]
    public void NormalizeWindowsPath_handles_empty_and_whitespace()
    {
        Assert.Equal(string.Empty, ShellOpenPolicy.NormalizeWindowsPath(""));
        Assert.Equal(string.Empty, ShellOpenPolicy.NormalizeWindowsPath("   "));
        Assert.Equal(string.Empty, ShellOpenPolicy.NormalizeWindowsPath(null!));
    }

    [Fact]
    public void ToFileUri_percent_encodes_spaces()
    {
        string uri = ShellOpenPolicy.ToFileUri(@"C:\users\steamuser\AppData\Among Us\BetterDataV2.json");
        Assert.Equal("file:///C:/users/steamuser/AppData/Among%20Us/BetterDataV2.json", uri);
    }

    [Fact]
    public void Wine_attempts_start_with_winebrowser_file_uri()
    {
        var attempts = ShellOpenPolicy.BuildLaunchAttempts(
            @"C:\users\steamuser\Videos\match video.mp4", isDirectory: false, isWindows: true, isWineOrProton: true);

        Assert.Equal(2, attempts.Count);
        Assert.Equal("winebrowser.exe", attempts[0].FileName);
        Assert.Equal("\"file:///C:/users/steamuser/Videos/match%20video.mp4\"", attempts[0].Arguments);
        Assert.False(attempts[0].UseShellExecute);
        Assert.Equal("cmd.exe", attempts[1].FileName);
        Assert.Contains("start", attempts[1].Arguments);
    }

    [Fact]
    public void Wine_directory_attempts_include_explorer_fallback()
    {
        var attempts = ShellOpenPolicy.BuildLaunchAttempts(
            "C:/users/steamuser/Replays", isDirectory: true, isWindows: true, isWineOrProton: true);

        Assert.Equal(3, attempts.Count);
        Assert.Equal("winebrowser.exe", attempts[0].FileName);
        Assert.Equal("explorer.exe", attempts[1].FileName);
        Assert.Equal("\"C:\\users\\steamuser\\Replays\"", attempts[1].Arguments);
        Assert.Equal("cmd.exe", attempts[2].FileName);
    }

    [Fact]
    public void Native_windows_uses_shell_execute_then_explorer_select()
    {
        var attempts = ShellOpenPolicy.BuildLaunchAttempts(
            @"C:\Replays\match.mp4", isDirectory: false, isWindows: true, isWineOrProton: false);

        Assert.Equal(2, attempts.Count);
        Assert.Equal(@"C:\Replays\match.mp4", attempts[0].FileName);
        Assert.Equal(string.Empty, attempts[0].Arguments);
        Assert.True(attempts[0].UseShellExecute);
        Assert.Equal("explorer.exe", attempts[1].FileName);
        Assert.Equal("/select,\"C:\\Replays\\match.mp4\"", attempts[1].Arguments);
    }

    [Fact]
    public void Native_windows_directory_explorer_fallback_has_no_select()
    {
        var attempts = ShellOpenPolicy.BuildLaunchAttempts(
            @"C:\Replays", isDirectory: true, isWindows: true, isWineOrProton: false);

        Assert.Equal("explorer.exe", attempts[1].FileName);
        Assert.Equal("\"C:\\Replays\"", attempts[1].Arguments);
    }

    [Fact]
    public void Empty_path_returns_no_attempts()
    {
        Assert.Empty(ShellOpenPolicy.BuildLaunchAttempts("", isDirectory: false, isWindows: true, isWineOrProton: false));
        Assert.Empty(ShellOpenPolicy.BuildLaunchAttempts("  ", isDirectory: true, isWindows: true, isWineOrProton: true));
    }
}
