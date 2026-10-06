using System.Text.RegularExpressions;

namespace HoryTweaks.Core.Diagnostics;

/// <summary>
/// Removes identifying values from text before it is written to a shareable diagnostic report.
/// </summary>
internal static class DiagnosticRedactor
{
    internal const string FriendCodePlaceholder = "<friend-code>";
    internal const string LobbyCodePlaceholder = "<lobby-code>";
    internal const string AddressPlaceholder = "<address>";
    internal const string UserPlaceholder = "<user>";
    internal const string SecretPlaceholder = "<redacted>";
    internal const string PrivateEntryPlaceholder = "<private log entry omitted>";

    private static readonly Regex FriendCode = new(@"\b[A-Za-z]{2,}#\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex LobbyCode = new(@"\b[A-Z]{6}\b", RegexOptions.Compiled);
    private static readonly Regex JoinedLobbyCode = new(@"(?<=\b(?i:joined|code|lobby)\W{1,3})[A-Z]{4,6}\b", RegexOptions.Compiled);
    private static readonly Regex Ipv4Address = new(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b(?::\d{1,5})?", RegexOptions.Compiled);
    private static readonly Regex UserProfilePath = new(@"(?<=[\\/](?i:Users|home)[\\/])[^\\/\r\n""'<>|]+", RegexOptions.Compiled);
    private static readonly Regex SecretAssignment = new(@"(?i)\b(secret|token|password|passwd|api[_-]?key|auth(?:orization)?)\b(\s*[:=]\s*)\S+", RegexOptions.Compiled);

    /// <summary>
    /// Redacts friend codes, lobby codes, network addresses, user profile names,
    /// obvious secret assignments and any explicitly supplied sensitive values.
    /// </summary>
    internal static string Redact(string? text, IEnumerable<string>? sensitiveValues = null)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var result = text;

        if (sensitiveValues != null)
        {
            foreach (var value in sensitiveValues.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).OrderByDescending(v => v.Length))
            {
                result = result.Replace(value, SecretPlaceholder, StringComparison.OrdinalIgnoreCase);
            }
        }

        result = SecretAssignment.Replace(result, m => $"{m.Groups[1].Value}{m.Groups[2].Value}{SecretPlaceholder}");
        result = FriendCode.Replace(result, FriendCodePlaceholder);
        result = JoinedLobbyCode.Replace(result, LobbyCodePlaceholder);
        result = LobbyCode.Replace(result, LobbyCodePlaceholder);
        result = Ipv4Address.Replace(result, AddressPlaceholder);
        result = UserProfilePath.Replace(result, UserPlaceholder);

        return result;
    }

    /// <summary>
    /// Replaces log lines that carry an encrypted private entry with a placeholder so the
    /// decrypted content (player names, chat, gameplay events) never reaches the report.
    /// </summary>
    internal static string OmitPrivateEntries(string? logText, char privatePrefix, char privatePostfix)
    {
        if (string.IsNullOrEmpty(logText))
            return string.Empty;

        var lines = logText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var start = line.IndexOf(privatePrefix);
            if (start < 0)
                continue;

            var end = line.IndexOf(privatePostfix, start + 1);
            var trailing = line.EndsWith('\r') ? "\r" : string.Empty;
            lines[i] = end < 0
                ? line[..start] + PrivateEntryPlaceholder + trailing
                : line[..start] + PrivateEntryPlaceholder + line[(end + 1)..];
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Returns the last <paramref name="maxLines"/> non-empty-trailing lines of <paramref name="text"/>.
    /// </summary>
    internal static IReadOnlyList<string> TakeRecentLines(string? text, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0)
            return [];

        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        if (lines.Count <= maxLines)
            return lines;

        return lines.GetRange(lines.Count - maxLines, maxLines);
    }
}
