namespace BetterAmongUs.Modules;

/// <summary>
/// How a local disconnect should be treated by the auto-rejoin routine.
/// </summary>
internal enum DisconnectKind
{
    /// The player caused the disconnect (leaving, closing, backgrounding the app).
    Voluntary,

    /// The player was removed or rejected in a way retrying cannot fix
    /// (kicked, banned, sanction, version or permission blocks).
    Removal,

    /// Network, server or game-state failures a retry may still resolve.
    Transient
}

/// <summary>
/// Unity-independent decisions for the auto-rejoin routine: which disconnect
/// reasons start or stop it, and how long it retries for.
/// </summary>
internal static class RejoinPolicy
{
    /// <summary>Total window in seconds during which rejoin is attempted.</summary>
    internal const float RejoinWindowSeconds = 30f;

    /// <summary>Seconds between join attempts inside the rejoin window.</summary>
    internal const float AttemptIntervalSeconds = 4f;

    /// <summary>Seconds after which a join attempt with no result is abandoned and retried.</summary>
    internal const float AttemptTimeoutSeconds = 12f;

    // Reason codes mirror the game's DisconnectReasons enum; stored as ints so this
    // class stays free of IL2CPP/Unity references and can be unit tested directly.
    internal static DisconnectKind Classify(int reason)
        => reason switch
        {
            0 // ExitGame — player left on purpose
            or 16 // Destroy — session torn down locally
            or 207 // FocusLostBackground — app backgrounded by the user
            or 208 // IntentionalLeaving
            or 209 // FocusLost
            or 210 // NewConnection — this account connected elsewhere
                => DisconnectKind.Voluntary,

            5 // IncorrectVersion
            or 6 // Banned
            or 7 // Kicked
            or 8 // Custom — host-driven removal messages
            or 9 // InvalidName
            or 10 // Hacking
            or 11 // NotAuthorized
            or 21 // MismatchedVersion
            or 103 // PlatformLock
            or 106 // InvalidGameOptions
            or 110 // QuickchatLock
            or 112 // Sanctions
            or 114 // SelfPlatformLock
            or 211 // PlatformParentalControlsBlock
            or 212 // PlatformUserBlock
                => DisconnectKind.Removal,

            _ => DisconnectKind.Transient,
        };

    /// <summary>Whether a disconnect with this reason should start the auto-rejoin routine.</summary>
    internal static bool ShouldAttemptRejoin(int reason) => Classify(reason) == DisconnectKind.Transient;

    /// <summary>Whether a disconnect arriving while a rejoin is running should stop it.</summary>
    internal static bool ShouldCancelRejoin(int reason) => Classify(reason) != DisconnectKind.Transient;
}
