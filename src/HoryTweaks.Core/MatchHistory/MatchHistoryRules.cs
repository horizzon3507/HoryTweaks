namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Anti-cheat gates and recording limits. Recorded data must never be readable while a match runs.
/// </summary>
internal static class MatchHistoryRules
{
    /// <summary>
    /// Interval between position samples.
    /// </summary>
    internal const int SampleIntervalMs = 200;

    /// <summary>
    /// Recording stops adding data after this duration; the match is still saved.
    /// </summary>
    internal const int MaxDurationMs = 3_600_000;

    /// <summary>
    /// Maximum number of events stored for one match.
    /// </summary>
    internal const int MaxEvents = 5000;

    /// <summary>
    /// Determines whether saved matches may be read or shown. Denied during a match and without a client.
    /// </summary>
    internal static bool CanView(ClientGameState? state)
    {
        return state is ClientGameState.NotJoined or ClientGameState.Joined or ClientGameState.Ended;
    }

    /// <summary>
    /// Determines whether the recorder should start capturing the current match.
    /// </summary>
    internal static bool ShouldBeginRecording(bool enabled, bool armed, bool freePlay, ClientGameState? state, bool shipPresent, bool introPlaying)
    {
        return enabled && armed && !freePlay && state == ClientGameState.Started && shipPresent && !introPlaying;
    }
}
