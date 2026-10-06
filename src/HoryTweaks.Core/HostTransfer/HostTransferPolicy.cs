namespace BetterAmongUs.Core.HostTransfer;

/// <summary>
/// Reasons a host transfer is refused.
/// </summary>
internal enum HostTransferDenial
{
    None,
    NotHost,
    NotInGame,
    GameEnded,
    GameStarting,
    NotInLobby,
    TargetMissing,
    TargetIsDummy,
    TargetIsLocal,
    TargetIsHost,
    TargetNotReady,
    NoEligibleTarget
}

/// <summary>
/// Snapshot of the host, game and target state used to decide whether a transfer may run.
/// </summary>
internal readonly record struct HostTransferContext(
    bool IsHost,
    bool IsInGame,
    bool IsEnded,
    bool IsStarting,
    bool IsLobby,
    bool RequireLobby,
    bool TargetExists,
    bool TargetIsDummy,
    bool TargetIsLocal,
    bool TargetIsHost,
    bool TargetDataCollected);

/// <summary>
/// A player considered as an automatic host-transfer successor.
/// </summary>
internal readonly record struct HostTransferCandidate(
    int ClientId,
    bool IsDummy,
    bool IsLocal,
    bool IsHost,
    bool IsReady);

/// <summary>
/// Pure host-transfer decision logic shared by the /transferhost command and the AFK auto-transfer.
/// </summary>
internal static class HostTransferPolicy
{
    internal static HostTransferDenial Evaluate(in HostTransferContext context)
    {
        if (!context.IsHost)
            return HostTransferDenial.NotHost;

        if (!context.IsInGame)
            return HostTransferDenial.NotInGame;

        if (context.IsEnded)
            return HostTransferDenial.GameEnded;

        if (context.IsStarting)
            return HostTransferDenial.GameStarting;

        if (context.RequireLobby && !context.IsLobby)
            return HostTransferDenial.NotInLobby;

        if (!context.TargetExists)
            return HostTransferDenial.TargetMissing;

        if (context.TargetIsDummy)
            return HostTransferDenial.TargetIsDummy;

        if (context.TargetIsLocal)
            return HostTransferDenial.TargetIsLocal;

        if (context.TargetIsHost)
            return HostTransferDenial.TargetIsHost;

        if (!context.TargetDataCollected)
            return HostTransferDenial.TargetNotReady;

        return HostTransferDenial.None;
    }

    /// <summary>
    /// Among Us assigns client ids in join order, so the lowest eligible id belongs to the
    /// player who has been in the lobby longest. Returns -1 when nobody is eligible.
    /// </summary>
    internal static int PickSuccessorClientId(IReadOnlyList<HostTransferCandidate> candidates)
    {
        int best = -1;

        foreach (var candidate in candidates)
        {
            if (candidate.ClientId < 0 ||
                candidate.IsDummy ||
                candidate.IsLocal ||
                candidate.IsHost ||
                !candidate.IsReady)
                continue;

            if (best < 0 || candidate.ClientId < best)
                best = candidate.ClientId;
        }

        return best;
    }

    /// <summary>
    /// The AFK auto-transfer only fires once the configured whole minutes of inactivity elapsed.
    /// </summary>
    internal static bool ShouldTransferOnAfk(double idleSeconds, int afkMinutes) =>
        afkMinutes > 0 && idleSeconds >= afkMinutes * 60.0;

    /// <summary>
    /// Whether a received host-transfer announcement may be applied. Accepted when the sender is
    /// the client currently believed to be host, or when the local HostId already moved to the
    /// announced target — on locally hosted games the genuine migration broadcast can land before
    /// the CustomRPC, and re-applying the same transfer is idempotent. A non-host announcing a
    /// different target is rejected.
    /// </summary>
    internal static bool AcceptsRemoteTransfer(int senderClientId, int targetClientId, int currentHostId) =>
        targetClientId >= 0 && (senderClientId == currentHostId || targetClientId == currentHostId);
}
