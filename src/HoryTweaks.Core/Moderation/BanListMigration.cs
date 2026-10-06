using System.Text.RegularExpressions;

namespace BetterAmongUs.Core.Moderation;

/// <summary>
/// Pure conversion rules for migrating legacy wildcard ban lists to regex patterns.
/// Legacy entries use <c>**</c> at the start and/or end to mean "ends with", "starts with" or "contains".
/// </summary>
internal static class BanListMigration
{
    /// <summary>
    /// Determines whether a ban list line is a pattern rather than a blank line or comment.
    /// </summary>
    internal static bool IsPatternLine(string line)
    {
        return !string.IsNullOrWhiteSpace(line) &&
               !line.StartsWith("//") &&
               !line.StartsWith("#");
    }

    /// <summary>
    /// Determines whether a pattern still uses the legacy <c>**</c> wildcard syntax.
    /// </summary>
    internal static bool UsesWildcard(string line)
    {
        return Regex.IsMatch(line, @"\*\*");
    }

    /// <summary>
    /// Converts a legacy wildcard chat filter into a case-insensitive, word-bounded regex.
    /// </summary>
    internal static string ChatWildcardToRegex(string line)
    {
        return "(?i)(?: |^)" +
               (line.StartsWith("**") ? ".*" : string.Empty) +
               Regex.Escape(line.Trim('*')) +
               (line.EndsWith("**") ? ".*" : string.Empty) +
               "(?: |$)";
    }

    /// <summary>
    /// Converts a legacy wildcard name filter into a case-insensitive, anchored regex.
    /// </summary>
    internal static string NameWildcardToRegex(string line)
    {
        return "(?i)" +
               (line.StartsWith("**") ? string.Empty : "^") +
               Regex.Escape(line.Trim('*')) +
               (line.EndsWith("**") ? string.Empty : "$");
    }
}
