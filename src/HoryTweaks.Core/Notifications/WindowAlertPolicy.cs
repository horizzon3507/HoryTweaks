namespace HoryTweaks.Core.Notifications;

/// <summary>
/// Events that can trigger an unfocused-window alert (taskbar flash + subtle chime).
/// </summary>
internal enum WindowAlertReason
{
    LobbyFull,
    GameStarted,
    MeetingStarted
}

/// <summary>
/// Pure rate-limit and rising-edge rules for unfocused window alerts.
/// </summary>
internal sealed class WindowAlertPolicy
{
    internal const double DefaultCooldownSeconds = 2.5;

    private double _lastAlertTime = double.NegativeInfinity;
    private bool _wasLobbyFull;

    internal bool ShouldAlert(bool enabled, bool isWindowFocused, double nowSeconds, double cooldownSeconds = DefaultCooldownSeconds)
    {
        if (!enabled || isWindowFocused)
            return false;

        if (nowSeconds - _lastAlertTime < cooldownSeconds)
            return false;

        _lastAlertTime = nowSeconds;
        return true;
    }

    /// <summary>
    /// Tracks lobby occupancy and returns true only on the rising edge when the lobby becomes full.
    /// </summary>
    internal bool ObserveLobbyOccupancy(int playerCount, int maxPlayers)
    {
        if (maxPlayers <= 1)
        {
            _wasLobbyFull = false;
            return false;
        }

        bool isFull = playerCount >= maxPlayers;
        bool becameFull = isFull && !_wasLobbyFull;
        _wasLobbyFull = isFull;
        return becameFull;
    }

    internal void ResetLobby()
    {
        _wasLobbyFull = false;
    }
}
