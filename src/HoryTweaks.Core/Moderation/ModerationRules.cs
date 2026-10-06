namespace HoryTweaks.Core.Moderation;

/// <summary>
/// Moderation actions a host can take against another player.
/// </summary>
internal enum ModerationActionKind
{
    Kick,
    Ban
}

/// <summary>
/// Reasons a moderation action is refused.
/// </summary>
internal enum ModerationDenial
{
    None,
    NotHost,
    NotInGame,
    GameStarting,
    GameEnded,
    TargetMissing,
    TargetIsDummy,
    TargetIsSelf,
    TargetIsHost,
    TargetNotReady
}

/// <summary>
/// Snapshot of the host, game and target state used to decide whether an action may run.
/// </summary>
internal readonly record struct ModerationContext(
    bool IsHost,
    bool IsInGame,
    bool IsEnded,
    bool IsStarting,
    bool TargetExists,
    bool TargetIsDummy,
    bool TargetIsLocal,
    bool TargetIsHost,
    bool TargetDataCollected);

/// <summary>
/// Pure host and game-state checks shared by the moderation center actions.
/// </summary>
internal static class ModerationRules
{
    internal static ModerationDenial Evaluate(in ModerationContext context)
    {
        if (!context.IsHost)
            return ModerationDenial.NotHost;

        if (!context.IsInGame)
            return ModerationDenial.NotInGame;

        if (context.IsEnded)
            return ModerationDenial.GameEnded;

        if (context.IsStarting)
            return ModerationDenial.GameStarting;

        if (!context.TargetExists)
            return ModerationDenial.TargetMissing;

        if (context.TargetIsDummy)
            return ModerationDenial.TargetIsDummy;

        if (context.TargetIsLocal)
            return ModerationDenial.TargetIsSelf;

        if (context.TargetIsHost)
            return ModerationDenial.TargetIsHost;

        if (!context.TargetDataCollected)
            return ModerationDenial.TargetNotReady;

        return ModerationDenial.None;
    }
}
