namespace BetterAmongUs.Utilities;

/// <summary>
/// Query matching and row visibility logic for the settings search field in the
/// Tweaks tab. Kept free of Unity types so the unit tests can exercise it.
/// </summary>
internal static class SettingsSearchFilter
{
    /// <summary>
    /// A single row of the option list as <see cref="ComputeVisible"/> sees it:
    /// headers, titles and dividers are group labels, every other row is an option.
    /// </summary>
    internal readonly record struct Row(bool IsGroupLabel, bool NormallyVisible, string? Title, string? Description);

    /// <summary>
    /// Determines whether the query actually filters anything.
    /// </summary>
    internal static bool IsActive(string? query) => !string.IsNullOrWhiteSpace(query);

    /// <summary>
    /// Returns whether the trimmed query appears in the title or the description,
    /// case-insensitively. An empty or whitespace-only query matches every row.
    /// </summary>
    internal static bool Matches(string? query, string? title, string? description)
    {
        if (!IsActive(query))
            return true;

        var needle = query!.Trim();
        return Contains(title, needle) || Contains(description, needle);
    }

    /// <summary>
    /// Computes which rows stay visible for a query. Rows that are not normally
    /// visible stay hidden; group labels hide once every option between them and
    /// the next group label is filtered out.
    /// </summary>
    internal static bool[] ComputeVisible(IReadOnlyList<Row> rows, string? query)
    {
        var visible = new bool[rows.Count];
        var filtering = IsActive(query);
        var groupHasVisibleOption = false;

        for (var i = rows.Count - 1; i >= 0; i--)
        {
            var row = rows[i];
            if (!row.NormallyVisible)
            {
                continue;
            }

            if (row.IsGroupLabel)
            {
                visible[i] = !filtering || groupHasVisibleOption;
                groupHasVisibleOption = false;
            }
            else
            {
                visible[i] = !filtering || Matches(query, row.Title, row.Description);
                groupHasVisibleOption |= visible[i];
            }
        }

        return visible;
    }

    private static bool Contains(string? text, string needle) =>
        text?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;
}
