namespace HoryTweaks.Core.Diagnostics;

/// <summary>
/// Finds a legacy BetterAmongUs installation that would load beside HoryTweaks.
/// Detection never modifies or removes the files it finds.
/// </summary>
internal static class LegacyPluginDetector
{
    internal const string LegacyPluginGuid = "com.d1gq.betteramongus";
    internal const string LegacyPluginFileName = "BetterAmongUs.dll";

    private static readonly EnumerationOptions SearchOptions = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    /// <summary>
    /// Returns every <c>BetterAmongUs.dll</c> below <paramref name="pluginsDirectory"/>, sorted for stable output.
    /// Missing or unreadable directories yield an empty list.
    /// </summary>
    internal static IReadOnlyList<string> FindLegacyPluginFiles(string? pluginsDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
            return [];

        try
        {
            return Directory.EnumerateFiles(pluginsDirectory, LegacyPluginFileName, SearchOptions)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Returns whether a loaded plugin GUID belongs to the legacy BetterAmongUs plugin.
    /// </summary>
    internal static bool IsLegacyPluginGuid(string? guid)
    {
        return string.Equals(guid, LegacyPluginGuid, StringComparison.OrdinalIgnoreCase);
    }
}
