using System.Text;

namespace HoryTweaks.Core.Platform;

/// <summary>
/// One candidate process launch for opening a file or folder in the desktop shell.
/// </summary>
internal readonly record struct ShellLaunchAttempt(string FileName, string Arguments, bool UseShellExecute);

/// <summary>
/// Pure, unit-tested cross-platform path normalization and shell-launch candidate generator.
/// On Wine/Proton the first candidate is always <c>winebrowser.exe</c> with a file URI, which routes
/// to the Linux desktop's native xdg-open and bypasses Wine's missing file associations entirely.
/// </summary>
internal static class ShellOpenPolicy
{
    /// <summary>
    /// Normalizes a Windows or Wine path: forward slashes become backslashes, duplicate interior
    /// separators collapse (preserving a leading UNC <c>\\</c>), and trailing separators are removed
    /// unless the path is a drive root like <c>C:\</c>.
    /// </summary>
    internal static string NormalizeWindowsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string trimmed = path.Trim().Replace('/', '\\');

        bool unc = trimmed.StartsWith("\\\\", StringComparison.Ordinal);
        var sb = new StringBuilder(trimmed.Length);
        bool prevSlash = false;
        foreach (char c in trimmed)
        {
            if (c == '\\')
            {
                if (prevSlash)
                    continue;
                prevSlash = true;
            }
            else
            {
                prevSlash = false;
            }

            sb.Append(c);
        }

        string collapsed = sb.ToString();
        if (unc && !collapsed.StartsWith("\\\\", StringComparison.Ordinal))
        {
            collapsed = "\\" + collapsed;
        }

        while (collapsed.Length > 3 && collapsed.EndsWith('\\'))
        {
            collapsed = collapsed[..^1];
        }

        // A bare drive root like "C:\" must keep its trailing slash; "C:" alone is invalid as a target.
        if (collapsed.Length == 2 && collapsed[1] == ':')
        {
            collapsed += "\\";
        }

        return collapsed;
    }

    /// <summary>
    /// Converts a normalized Windows path into a percent-encoded <c>file:///</c> URI suitable for winebrowser.
    /// </summary>
    internal static string ToFileUri(string normalizedWindowsPath)
    {
        try
        {
            return new Uri(normalizedWindowsPath).AbsoluteUri;
        }
        catch (UriFormatException)
        {
            // Manual fallback: forward slashes plus minimal percent-encoding.
            var sb = new StringBuilder("file:///");
            foreach (char c in normalizedWindowsPath.Replace('\\', '/'))
            {
                if (char.IsLetterOrDigit(c) || c is '/' or '.' or '-' or '_' or '~' or ':')
                {
                    sb.Append(c);
                }
                else
                {
                    foreach (byte b in Encoding.UTF8.GetBytes(new[] { c }))
                    {
                        sb.Append('%').Append(b.ToString("X2"));
                    }
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Builds the ordered shell-launch candidates for opening <paramref name="path"/>.
    /// </summary>
    internal static IReadOnlyList<ShellLaunchAttempt> BuildLaunchAttempts(string path, bool isDirectory, bool isWindows, bool isWineOrProton)
    {
        string normalized = NormalizeWindowsPath(path);
        if (normalized.Length == 0)
            return Array.Empty<ShellLaunchAttempt>();

        var attempts = new List<ShellLaunchAttempt>();

        if (isWineOrProton)
        {
            // winebrowser forwards file URIs to the host Linux desktop's xdg-open, so folders open in
            // the native file manager, mp4s in the native video player and json/txt in the native editor.
            attempts.Add(new ShellLaunchAttempt("winebrowser.exe", $"\"{ToFileUri(normalized)}\"", UseShellExecute: false));
            if (isDirectory)
            {
                attempts.Add(new ShellLaunchAttempt("explorer.exe", $"\"{normalized}\"", UseShellExecute: false));
            }

            attempts.Add(new ShellLaunchAttempt("cmd.exe", $"/c start \"\" \"{normalized}\"", UseShellExecute: false));
            return attempts;
        }

        if (isWindows)
        {
            attempts.Add(new ShellLaunchAttempt(normalized, string.Empty, UseShellExecute: true));
            attempts.Add(new ShellLaunchAttempt(
                "explorer.exe",
                isDirectory ? $"\"{normalized}\"" : $"/select,\"{normalized}\"",
                UseShellExecute: false));
            return attempts;
        }

        string opener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        attempts.Add(new ShellLaunchAttempt(opener, $"\"{path.Trim()}\"", UseShellExecute: false));
        return attempts;
    }
}
