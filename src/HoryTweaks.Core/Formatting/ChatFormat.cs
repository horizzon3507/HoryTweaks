using System.Text;

namespace BetterAmongUs.Core.Formatting;

/// <summary>
/// Unity-independent helpers for formatting chat text (plain-text export).
/// </summary>
internal static class ChatFormat
{
    /// <summary>
    /// Removes TextMeshPro rich-text tags, leaving plain text.
    /// </summary>
    internal static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        StringBuilder sb = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<' && TryReadTag(text, i, out int tagEnd))
            {
                i = tagEnd;
                continue;
            }

            sb.Append(text[i]);
        }

        return sb.ToString();
    }

    // A TMP tag looks like "<name ...>" or "</name>": '<', optional '/', letter, then no '<' or newline until '>'.
    // Anything else (e.g. "<3", "a < b") is treated as literal text.
    private static bool TryReadTag(string text, int start, out int tagEnd)
    {
        tagEnd = -1;
        int i = start + 1;
        if (i < text.Length && text[i] == '/')
            i++;

        if (i >= text.Length || !char.IsLetter(text[i]))
            return false;

        while (i < text.Length && text[i] != '>')
        {
            if (text[i] == '<' || text[i] == '\n')
                return false;

            i++;
        }

        if (i >= text.Length)
            return false;

        tagEnd = i;
        return true;
    }
}
