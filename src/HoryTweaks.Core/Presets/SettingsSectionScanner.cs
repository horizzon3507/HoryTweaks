namespace BetterAmongUs.Core.Presets;

/// <summary>
/// Scans the flattened <c>key/value|key/value</c> settings representation.
/// Separators are only recognized outside JSON strings, so values may contain
/// '|' and '/' characters when quoted, and escaped characters inside strings
/// are honored.
/// </summary>
internal static class SettingsSectionScanner
{
    private const char PairSeparator = '|';
    private const char KeyValueSeparator = '/';

    /// <summary>
    /// A located key/value pair inside the flattened text, expressed as offsets
    /// and lengths into the source string.
    /// </summary>
    internal readonly record struct SettingsSection(int KeyOffset, int KeyLength, int ValueOffset, int ValueLength)
    {
        /// <summary>The key slice of <paramref name="flattened"/>.</summary>
        internal ReadOnlySpan<char> Key(string flattened) => flattened.AsSpan(KeyOffset, KeyLength);

        /// <summary>The raw value slice of <paramref name="flattened"/>.</summary>
        internal ReadOnlySpan<char> Value(string flattened) => flattened.AsSpan(ValueOffset, ValueLength);
    }

    /// <summary>
    /// Enumerates each non-empty key/value pair. Pairs without a
    /// <c>/</c> separator outside JSON strings are skipped.
    /// </summary>
    internal static IEnumerable<SettingsSection> Scan(string flattened)
    {
        int segmentStart = 0;
        int separator = -1;
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i <= flattened.Length; i++)
        {
            char c = i < flattened.Length ? flattened[i] : '\0';

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
            }
            else if (c == '"')
            {
                inString = true;
            }
            else if (c == KeyValueSeparator && separator < 0)
            {
                separator = i;
            }

            if ((c == PairSeparator && !inString) || i == flattened.Length)
            {
                int segmentEnd = i;
                if (segmentEnd > segmentStart && separator >= 0)
                {
                    int valueStart = separator + 1;
                    yield return new SettingsSection(
                        segmentStart,
                        separator - segmentStart,
                        valueStart,
                        segmentEnd - valueStart);
                }

                segmentStart = i + 1;
                separator = -1;
            }
        }
    }
}
