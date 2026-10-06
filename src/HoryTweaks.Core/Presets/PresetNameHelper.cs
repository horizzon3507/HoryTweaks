using System.Text.RegularExpressions;

namespace BetterAmongUs.Core.Presets;

/// <summary>
/// Normalizes user supplied preset names before they are stored in a settings file.
/// </summary>
internal static class PresetNameHelper
{
    internal const string SettingKey = "PresetName";
    internal const int MaxLength = 20;

    private static readonly Regex RichTextTags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Strips rich text tags and surplus whitespace and clamps the length.
    /// </summary>
    /// <returns>The cleaned name, or null when nothing usable remains.</returns>
    internal static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string cleaned = RichTextTags.Replace(raw, string.Empty);
        cleaned = Whitespace.Replace(cleaned, " ").Trim();

        if (cleaned.Length > MaxLength)
            cleaned = cleaned[..MaxLength].TrimEnd();

        return cleaned.Length == 0 ? null : cleaned;
    }
}
