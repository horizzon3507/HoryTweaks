namespace BetterAmongUs.Enums;

/// <summary>
/// Defines custom Remote Procedure Call (RPC) identifiers used by BetterAmongUs.
/// </summary>
internal enum CustomRPC : int
{
    /// <summary>
    /// RPC for sending a shared secret to another player.
    /// </summary>
    SendSecretToPlayer = 151,

    /// <summary>
    /// RPC for checking the hash of a shared secret received from another player.
    /// </summary>
    CheckSecretHashFromPlayer = 152,
}