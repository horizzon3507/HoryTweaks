namespace BetterAmongUs.Modules.Support;

/// <summary>
/// Identifies which BAU-family mod a remote player is running.
/// </summary>
internal enum ModdedUserKind
{
    /// <summary>No known BAU-family mod was detected for this player.</summary>
    None,

    /// <summary>
    /// The player completed the BetterAmongUs handshake but did not advertise HoryTweaks
    /// (an upstream BetterAmongUs user, or another BAU-family mod).
    /// </summary>
    BetterUser,

    /// <summary>The player advertised the HoryTweaks mod flag.</summary>
    HoryUser,
}

/// <summary>
/// Unity-independent wire constants and classification helpers for the modded-support handshake.
/// </summary>
internal static class ModdedUserClassification
{
    /// <summary>
    /// Flag value a HoryTweaks client advertises in the <c>CustomRPC.AdvertiseHoryUser</c> RPC.
    /// Third-party mods may send the same value to identify as HoryTweaks-compatible
    /// (see examples/moddedsupport).
    /// </summary>
    internal const string HoryTweaksFlag = "mod.horytweaks";

    /// <summary>
    /// The wire hash of <see cref="HoryTweaksFlag"/>, produced by <see cref="GetFlagHash"/>.
    /// </summary>
    internal static int HoryTweaksFlagHash { get; } = GetFlagHash(HoryTweaksFlag);

    /// <summary>
    /// Checks whether a flag hash received on the wire is the HoryTweaks mod flag.
    /// </summary>
    internal static bool IsHoryTweaksFlagHash(int flagHash) => flagHash == HoryTweaksFlagHash;

    /// <summary>
    /// Classifies a player from the detected handshake flags. A HoryTweaks advertise wins
    /// over the plain BetterAmongUs handshake, since HoryTweaks peers also complete the
    /// BAU secret exchange.
    /// </summary>
    internal static ModdedUserKind Classify(bool isBetterUser, bool isHoryUser)
    {
        if (isHoryUser)
            return ModdedUserKind.HoryUser;
        if (isBetterUser)
            return ModdedUserKind.BetterUser;
        return ModdedUserKind.None;
    }

    /// <summary>
    /// Generates a integer hash from a string for use as a flag identifier.
    /// </summary>
    /// <param name="input">The input string to hash. Can be null or empty.</param>
    /// <returns>
    /// A integer hash value. Returns 0 for null or empty strings.
    /// </returns>
    internal static int GetFlagHash(string? input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        int hash = 17;
        hash = hash * 31 + input.Length;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            hash = hash * 31 + c;
            hash = hash * 31 + i;
        }

        hash = hash * 31 + input.Length;
        return hash;
    }
}
