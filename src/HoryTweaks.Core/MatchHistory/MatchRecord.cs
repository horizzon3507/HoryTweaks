namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Client-side game state, mirrored from <c>InnerNetClient.GameStates</c> so the rules stay Unity-free.
/// </summary>
internal enum ClientGameState
{
    NotJoined,
    Joined,
    Started,
    Ended
}

/// <summary>
/// Kinds of events stored in a match record. Values are persisted; never renumber.
/// </summary>
internal enum MatchEventKind
{
    Kill = 1,
    BodyReport = 2,
    EmergencyMeeting = 3,
    Vote = 4,
    MeetingResult = 5,
    VentEnter = 6,
    VentExit = 7,
    SabotageStart = 8,
    SabotageEnd = 9,
    Disconnect = 10,
    Shapeshift = 11,
    ShapeshiftRevert = 12,
    Vanish = 13,
    Appear = 14,
    MatchStart = 15,
    MeetingEnd = 16,
    Ejected = 17,
    Protect = 18,
    KillBlocked = 19,
    TasksDone = 20
}

/// <summary>
/// State of a player when the match ended.
/// </summary>
internal enum MatchPlayerState
{
    Alive = 0,
    Dead = 1,
    Disconnected = 2
}

/// <summary>
/// Per-sample player flags.
/// </summary>
[Flags]
internal enum MatchPositionFlags : byte
{
    None = 0,
    Dead = 1,
    InVent = 2
}

/// <summary>
/// Special player ids. They mirror <c>PlayerVoteArea.HasNotVoted/MissedVote/SkippedVote/DeadVote</c>.
/// </summary>
internal static class MatchIds
{
    internal const byte Unknown = 255;
    internal const byte Missed = 254;
    internal const byte Skipped = 253;
    internal const byte DeadVote = 252;
}

/// <summary>
/// A player of a recorded match. Only name, color and cosmetics identify other players.
/// </summary>
internal sealed record MatchPlayer(
    byte Id,
    string Name,
    int ColorId,
    int RoleType,
    bool IsImpostor,
    bool Won,
    bool IsLocal,
    MatchPlayerState FinalState,
    int Kills,
    int TasksDone,
    int TasksTotal,
    string HatId,
    string SkinId,
    string VisorId);

/// <summary>
/// A timeline event. <see cref="TimeMs"/> is relative to the start of recording.
/// </summary>
internal sealed record MatchEvent(int TimeMs, MatchEventKind Kind, byte Actor, byte Target, int Value, float X, float Y);

/// <summary>
/// One player's world position in a frame.
/// </summary>
internal readonly record struct MatchPosition(byte PlayerId, float X, float Y, MatchPositionFlags Flags);

/// <summary>
/// Positions of every connected player at one point in time.
/// </summary>
internal sealed record MatchFrame(int TimeMs, IReadOnlyList<MatchPosition> Positions);

/// <summary>
/// A complete recorded match.
/// </summary>
internal sealed record MatchRecord(
    DateTime StartedUtc,
    int DurationMs,
    byte MapId,
    float MapScale,
    bool MapFlipX,
    bool HideAndSeek,
    int GameOverReason,
    bool LocalWon,
    IReadOnlyList<MatchPlayer> Players,
    IReadOnlyList<MatchEvent> Events,
    IReadOnlyList<MatchFrame> Frames);
