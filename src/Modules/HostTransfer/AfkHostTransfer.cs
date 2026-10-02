using BetterAmongUs.Generated;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using BetterAmongUs.Utilities;
using UnityEngine;

namespace BetterAmongUs.Modules.HostTransfer;

/// <summary>
/// Tracks local input activity and hands the host crown to the longest-tenured connected player
/// when the local host goes AFK in a lobby for the configured minutes. Ticked from the existing
/// LobbyBehaviour.Update postfix so no extra per-frame polling is added.
/// </summary>
internal static class AfkHostTransfer
{
    private static float _lastActivityTime;
    private static Vector3 _lastMousePosition;

    private static bool OptionEnabled =>
        BetterGameSettings.HostTransferOnAfk != null && BetterGameSettings.HostTransferOnAfk.GetBool();

    internal static void Tick()
    {
        if (DetectsInput() || !OptionEnabled || !GameState.IsHost || !GameState.IsLobby)
        {
            _lastActivityTime = Time.time;
            return;
        }

        int afkMinutes = BetterGameSettings.HostTransferAfkMinutes?.GetInt() ?? 0;
        if (!HostTransferPolicy.ShouldTransferOnAfk(Time.time - _lastActivityTime, afkMinutes))
            return;

        _lastActivityTime = Time.time;

        var successor = HostTransfer.PickSuccessor();
        if (successor == null)
        {
            Utils.AddChatPrivate(TranslationStrings.HostTransfer_Denied_NoEligibleTarget.LocalizedString);
            return;
        }

        if (HostTransfer.Execute(successor, out var message, fromAfk: true))
            Utils.AddChatPrivate(message);
    }

    private static bool DetectsInput()
    {
        bool detected = Input.anyKeyDown || Input.mouseScrollDelta != Vector2.zero;

        var mousePosition = Input.mousePosition;
        if (mousePosition != _lastMousePosition)
        {
            detected = true;
            _lastMousePosition = mousePosition;
        }

        return detected;
    }
}
