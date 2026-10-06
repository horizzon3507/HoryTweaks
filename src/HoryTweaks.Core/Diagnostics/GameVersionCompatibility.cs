using System.Text.RegularExpressions;

namespace HoryTweaks.Core.Diagnostics;

/// <summary>
/// Describes how the running Among Us version relates to the versions the mod was built for.
/// </summary>
internal enum GameVersionStatus
{
    Unknown,
    Supported,
    Newer,
    Older
}

/// <summary>
/// Compares the running Among Us version with the supported versions encoded by the project.
/// </summary>
internal static class GameVersionCompatibility
{
    private static readonly Regex NumericPrefix = new(@"^\s*v?(\d+(?:\.\d+)+)", RegexOptions.Compiled);

    /// <summary>
    /// Compares <paramref name="currentVersion"/> against the range spanned by <paramref name="supportedVersions"/>.
    /// Versions that cannot be parsed yield <see cref="GameVersionStatus.Unknown"/> instead of throwing.
    /// </summary>
    internal static GameVersionStatus Compare(string? currentVersion, IReadOnlyList<string> supportedVersions)
    {
        if (!TryParse(currentVersion, out var current))
            return GameVersionStatus.Unknown;

        var supported = ParseAll(supportedVersions);
        if (supported.Count == 0)
            return GameVersionStatus.Unknown;

        if (current > supported[^1])
            return GameVersionStatus.Newer;

        if (current < supported[0])
            return GameVersionStatus.Older;

        return GameVersionStatus.Supported;
    }

    /// <summary>
    /// Formats the supported versions as a single version or as an "oldest - newest" range.
    /// </summary>
    internal static string FormatSupportedRange(IReadOnlyList<string> supportedVersions)
    {
        var supported = ParseAll(supportedVersions);
        if (supported.Count == 0)
            return string.Join(", ", supportedVersions);

        var oldest = supported[0];
        var newest = supported[^1];
        return oldest == newest ? oldest.ToString() : $"{oldest} - {newest}";
    }

    /// <summary>
    /// Parses the leading numeric part of a version string such as "2026.9.29" or "v2026.9.29s".
    /// </summary>
    internal static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var match = NumericPrefix.Match(text);
        if (!match.Success)
            return false;

        var numeric = match.Groups[1].Value;
        if (numeric.Count(c => c == '.') > 3)
            return false;

        return Version.TryParse(numeric, out version!);
    }

    private static List<Version> ParseAll(IReadOnlyList<string> supportedVersions)
    {
        var parsed = new List<Version>();
        foreach (var text in supportedVersions)
        {
            if (TryParse(text, out var version))
                parsed.Add(version);
        }

        parsed.Sort();
        return parsed;
    }
}
