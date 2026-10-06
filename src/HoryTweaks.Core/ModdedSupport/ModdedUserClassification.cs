namespace BetterAmongUs.Core.ModdedSupport;

/// <summary>
/// Identifies which BAU-family mod a remote player is running.
/// </summary>
internal enum ModdedUserKind
{
    /// <summary>No known BAU-family mod was detected for this player.</summary>
    None,

    /// <summary>
    /// The player completed the BetterAmongUs handshake but its handshake payload did not
    /// carry the HoryTweaks mark (an upstream BetterAmongUs user, or another BAU-family mod).
    /// </summary>
    BetterUser,

    /// <summary>The player's handshake payload carried the HoryTweaks mark.</summary>
    HoryUser,
}

/// <summary>
/// Unity-independent wire constants and classification helpers for the modded-support handshake.
/// </summary>
internal static class ModdedUserClassification
{
    /// <summary>
    /// Flag value a HoryTweaks client derives its handshake mark from.
    /// Third-party mods may send the same mark to identify as HoryTweaks-compatible
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
    /// Computes the mark a HoryTweaks client appends to its
    /// <c>CustomRPC.SendSecretToPlayer</c> handshake payload. The mark is derived from the
    /// temporary key of that handshake exchange, so it is a property of the handshake data
    /// itself rather than a standalone value peers can copy between messages.
    /// </summary>
    /// <param name="tempKey">The temporary key sent in the same handshake payload.</param>
    internal static int GetHoryTweaksHandshakeMark(int tempKey) => GetFlagHash($"{HoryTweaksFlag}:{tempKey}");

    /// <summary>
    /// Checks whether a mark appended to a peer's handshake payload identifies the peer as
    /// HoryTweaks. Only a payload carrying the mark computed from its own temporary key
    /// counts as a positive identification.
    /// </summary>
    /// <param name="tempKey">The temporary key read from the same handshake payload.</param>
    /// <param name="mark">The trailing mark read from the same handshake payload.</param>
    internal static bool IsHoryTweaksHandshakeMark(int tempKey, int mark) => mark == GetHoryTweaksHandshakeMark(tempKey);

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
