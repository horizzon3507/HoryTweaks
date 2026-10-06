namespace BetterAmongUs.Core.Moderation;

/// <summary>
/// Pure decision for the host's "kick players below minimum level" lobby rule,
/// extracted from CheckPlayerLevelPatch so it can be unit tested.
/// </summary>
internal static class LevelKickRules
{
    /// <summary>
    /// Whether the player should be kicked for being below the configured level.
    /// Reproduces the patch conditions exactly: a minimum player count only
    /// blocks the kick when it is above 1 and above the current player count.
    /// </summary>
    internal static bool ShouldKick(uint playerLevel, int kickLevelBelow, int minPlayers, int currentPlayers, bool isLocalPlayer, bool kickEnabled)
    {
        if (isLocalPlayer || !kickEnabled || playerLevel >= kickLevelBelow)
            return false;

        if (minPlayers > 1 && minPlayers > currentPlayers)
            return false;

        return true;
    }
}
