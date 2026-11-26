namespace HoryTweaks.Core.Chat;

/// <summary>
/// Session-scoped client-side chat mute list. Pure data structure kept in Core for unit testing.
/// </summary>
internal sealed class LocalMuteList
{
    private readonly Dictionary<byte, string> _muted = [];

    internal int Count => _muted.Count;

    internal bool IsMuted(byte playerId) => _muted.ContainsKey(playerId);

    internal bool Mute(byte playerId, string? displayName = null)
    {
        bool added = !_muted.ContainsKey(playerId);
        _muted[playerId] = string.IsNullOrWhiteSpace(displayName) ? $"ID{playerId}" : displayName.Trim();
        return added;
    }

    internal bool Unmute(byte playerId) => _muted.Remove(playerId);

    /// <summary>
    /// Toggles mute state for <paramref name="playerId"/> and returns <c>true</c> if the player is now muted.
    /// </summary>
    internal bool Toggle(byte playerId, string? displayName = null)
    {
        if (_muted.Remove(playerId))
            return false;

        _muted[playerId] = string.IsNullOrWhiteSpace(displayName) ? $"ID{playerId}" : displayName.Trim();
        return true;
    }

    internal int Clear()
    {
        int count = _muted.Count;
        _muted.Clear();
        return count;
    }

    internal IReadOnlyList<(byte PlayerId, string DisplayName)> Snapshot()
    {
        return [.. _muted.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, kvp.Value))];
    }
}
