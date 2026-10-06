namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Terminal states of an in-game update attempt.
/// </summary>
internal enum UpdateStatus
{
    /// <summary>
    /// The new assembly is installed and takes effect after a restart.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The full release package is staged and a helper applies it once the game exits.
    /// </summary>
    Staged,

    /// <summary>
    /// The verified release package was saved to disk; it has to be applied by hand.
    /// </summary>
    SavedToDisk,

    /// <summary>
    /// No internet connection could be confirmed.
    /// </summary>
    NoInternet,

    /// <summary>
    /// The update feed has no usable HTTPS download link.
    /// </summary>
    MissingDownloadLink,

    /// <summary>
    /// The download did not complete (network error, HTTP error, stall or timeout).
    /// </summary>
    DownloadFailed,

    /// <summary>
    /// The downloaded bytes are not a newer build of this mod.
    /// </summary>
    InvalidPayload,

    /// <summary>
    /// The file replacement failed; the previous assembly is still in place.
    /// </summary>
    InstallFailed,

    /// <summary>
    /// The update flow threw or ended without reporting a result.
    /// </summary>
    Unexpected,
}
