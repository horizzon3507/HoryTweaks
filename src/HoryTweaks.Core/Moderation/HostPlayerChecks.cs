namespace HoryTweaks.Core.Moderation;

/// <summary>
/// Pure host-side predicates for kicking a player, extracted from
/// PlayerControlUtils.CanKick so they can be unit tested.
/// </summary>
internal static class HostPlayerChecks
{
    /// <summary>
    /// Whether the target may be kicked right now, and whether that kick should
    /// also ban. All inputs are evaluated by the caller at call time, so a
    /// scheduled kick revalidates host authority and target state when it runs.
    /// </summary>
    internal static bool CanKick(bool isHost, bool isLocalPlayer, bool dataCollected, bool bypassDataCheck, bool isHostPlayer, bool isDummy, bool ban, bool forceBan, out bool shouldBan)
    {
        shouldBan = ban || forceBan;

        if (!isHost || isLocalPlayer || (!dataCollected && !bypassDataCheck) || isHostPlayer || isDummy)
            return false;

        return true;
    }
}
