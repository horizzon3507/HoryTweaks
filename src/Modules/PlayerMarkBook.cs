namespace BetterAmongUs.Modules;

/// <summary>
/// Status marks a player can carry as local suspicion bookkeeping.
/// </summary>
internal enum PlayerMarkStatus
{
    /// <summary>Marked as suspicious.</summary>
    Suspect = 0,

    /// <summary>Marked as cleared.</summary>
    Cleared = 1,

    /// <summary>Marked to investigate later.</summary>
    Investigate = 2,
}

/// <summary>
/// The marks on a single player: at most one role guess (as the role's int id) and one status.
/// </summary>
internal readonly record struct PlayerMark(int? RoleId, PlayerMarkStatus? Status)
{
    /// <summary>Whether no markers are set.</summary>
    internal bool IsEmpty => RoleId == null && Status == null;
}

/// <summary>
/// Local-only, session-scoped store of per-player marks. Never synced over the network.
/// </summary>
internal sealed class PlayerMarkBook
{
    private readonly Dictionary<byte, PlayerMark> _marks = [];

    /// <summary>Gets the marks for a player, or an empty mark.</summary>
    internal PlayerMark Get(byte playerId) =>
        _marks.TryGetValue(playerId, out var mark) ? mark : new PlayerMark();

    /// <summary>Applies a role guess, replaces a different guess, or clears the same guess.</summary>
    internal void ToggleRole(byte playerId, int roleId)
    {
        var mark = Get(playerId);
        Set(playerId, mark.RoleId == roleId
            ? mark with { RoleId = null }
            : mark with { RoleId = roleId });
    }

    /// <summary>Applies a status mark, replaces a different status, or clears the same status.</summary>
    internal void ToggleStatus(byte playerId, PlayerMarkStatus status)
    {
        var mark = Get(playerId);
        Set(playerId, mark.Status == status
            ? mark with { Status = null }
            : mark with { Status = status });
    }

    /// <summary>Removes every mark on a player.</summary>
    internal void ClearPlayer(byte playerId) => _marks.Remove(playerId);

    /// <summary>Removes every mark on every player (new match).</summary>
    internal void Reset() => _marks.Clear();

    /// <summary>Number of players currently carrying at least one mark.</summary>
    internal int MarkedCount => _marks.Count;

    private void Set(byte playerId, PlayerMark mark)
    {
        if (mark.IsEmpty)
            _marks.Remove(playerId);
        else
            _marks[playerId] = mark;
    }
}
