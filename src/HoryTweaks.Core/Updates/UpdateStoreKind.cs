namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Store variant of the current install, deciding which full release package applies.
/// </summary>
internal enum UpdateStoreKind
{
    /// <summary>
    /// The store could not be determined or has no full release package; the
    /// updater falls back to the assembly swap.
    /// </summary>
    Unknown,

    /// <summary>
    /// Steam, Epic Games or Microsoft Store install. These builds share the
    /// HoryTweaks-Steam-Epic-MsStore release package.
    /// </summary>
    SteamEpicMsStore,

    /// <summary>
    /// itch.io install, updated through the HoryTweaks-Itchio release package.
    /// </summary>
    Itchio,
}
