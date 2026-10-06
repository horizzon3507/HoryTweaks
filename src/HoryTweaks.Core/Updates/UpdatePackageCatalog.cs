namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Pure selection logic mapping a store variant to its advertised release package.
/// </summary>
internal static class UpdatePackageCatalog
{
    /// <summary>
    /// Returns the package link the feed advertises for <paramref name="storeKind"/>,
    /// or null when the feed does not carry a usable entry for it.
    /// </summary>
    internal static string? SelectLink(UpdatePackageLinks? packages, UpdateStoreKind storeKind)
    {
        if (packages == null)
        {
            return null;
        }

        var link = storeKind switch
        {
            UpdateStoreKind.SteamEpicMsStore => packages.SteamEpicMsStore,
            UpdateStoreKind.Itchio => packages.Itchio,
            _ => string.Empty,
        };

        return string.IsNullOrWhiteSpace(link) ? null : link;
    }

    /// <summary>
    /// Returns the file name a package URL points at, used to look the package up in the
    /// checksum manifest. Returns null when the URL path has no usable last segment.
    /// </summary>
    internal static string? AssetFileName(Uri packageUri)
    {
        var fileName = Uri.UnescapeDataString(packageUri.AbsolutePath).Split('/').Last();
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }
}
