using System.Reflection;

using BetterAmongUs.Generated;

using BetterAmongUs.Utilities;
using Hazel;
using InnerNet;
using UnityEngine;
using BetterAmongUs.Core.HostTransfer;
using BetterAmongUs.Features.Meeting;
using BetterAmongUs.Game;
using BetterAmongUs.Networking.Rpc;

namespace BetterAmongUs.Features.HostTransfer;

/// <summary>
/// Executes a voluntary host transfer initiated by the local host. Host migration in InnerNet is
/// only ever driven by a RemovePlayer broadcast from the authoritative server carrying the new
/// HostId, so a connected client cannot ask for host. This mirrors that real migration payload as
/// closely as the runtime allows: on locally hosted games the embedded InnerNetServer broadcasts
/// the genuine RemovePlayer migration message (reaching even unmodded LAN clients), and on every
/// game type the mod's CustomRPC channel distributes the transfer to modded clients, while the
/// local client applies the new HostId itself.
/// </summary>
internal static class HostTransfer
{
    internal static HostTransferDenial Check(PlayerControl? target, bool requireLobby = false)
    {
        bool exists = target != null && target.Data != null;

        return HostTransferPolicy.Evaluate(new HostTransferContext(
            IsHost: GameState.IsHost,
            IsInGame: GameState.IsInGame,
            IsEnded: GameState.IsEnded,
            IsStarting: GameState.IsCountDown || GameState.IsInIntro,
            IsLobby: GameState.IsLobby,
            RequireLobby: requireLobby,
            TargetExists: exists,
            TargetIsDummy: exists && target!.isDummy,
            TargetIsLocal: exists && target!.IsLocalPlayer(),
            TargetIsHost: exists && target!.IsHost(),
            TargetDataCollected: exists && target!.DataIsCollected()));
    }

    /// <summary>
    /// Runs the checks, then transfers host to the target. Returns false with an explanation when refused.
    /// </summary>
    internal static bool Execute(PlayerControl? target, out string message, bool fromAfk = false)
    {
        var denial = Check(target, requireLobby: fromAfk);
        if (denial != HostTransferDenial.None)
        {
            message = Describe(denial);
            return false;
        }

        Perform(target!.GetClientId());

        message = (fromAfk
                ? TranslationStrings.HostTransfer_AfkSuccess
                : TranslationStrings.HostTransfer_Success)
            .Format(target.GetPlayerNameAndColor());
        return true;
    }

    /// <summary>
    /// Picks the AFK successor the policy selects: the connected player who has been in the lobby longest.
    /// </summary>
    internal static PlayerControl? PickSuccessor()
    {
        var candidates = new List<HostTransferCandidate>(BAUPlugin.AllPlayerControls.Count);

        foreach (var player in BAUPlugin.AllPlayerControls)
        {
            if (player == null || player.Data == null)
                continue;

            candidates.Add(new HostTransferCandidate(
                player.GetClientId(),
                player.isDummy,
                player.IsLocalPlayer(),
                player.IsHost(),
                player.DataIsCollected()));
        }

        int clientId = HostTransferPolicy.PickSuccessorClientId(candidates);
        return clientId < 0 ? null : Utils.PlayerFromClientId(clientId);
    }

    internal static void Perform(int targetClientId)
    {
        // Locally hosted game: drive the real host-migration broadcast through the embedded server.
        TryLocalServerMigration(targetClientId);

        // Every game type: distribute the transfer to all modded clients over the CustomRPC channel.
        RPC.SendCustomRpcPacked(CustomRPC.TransferHost, writer => writer.Write(targetClientId));

        ApplyLocally(targetClientId);
    }

    /// <summary>
    /// Receiver side of the broadcast. Honored when the sender is the client we still believe is
    /// host, or when our HostId already moved to the announced target: on locally hosted games the
    /// genuine migration broadcast can land before our CustomRPC, and the application is idempotent.
    /// </summary>
    internal static void OnTransferReceived(PlayerControl sender, MessageReader reader)
    {
        int targetClientId = reader.ReadInt32();

        if (AmongUsClient.Instance == null || !HostTransferPolicy.AcceptsRemoteTransfer(
                sender.GetClientId(), targetClientId, AmongUsClient.Instance.HostId))
            return;

        // The announced successor must still be connected, or no client would end up as host.
        if (Utils.PlayerFromClientId(targetClientId) == null)
            return;

        ApplyLocally(targetClientId);
    }

    private static void ApplyLocally(int targetClientId)
    {
        if (AmongUsClient.Instance == null)
            return;

        var client = AmongUsClient.Instance;
        bool wasHost = client.AmHost;
        client.HostId = targetClientId;
        bool isHost = client.AmHost;

        if (isHost && !wasHost)
            Utils.AddChatPrivate(TranslationStrings.HostTransfer_Received.LocalizedString);

        if (wasHost != isHost)
            RefreshLocalHostRole(isHost);

        RefreshHostIndicators();
    }

    /// <summary>
    /// Replays the client-side effects a real migration produces. Becoming host runs the game's own
    /// OnBecomeHost hook so the client gains net-object authority and host bookkeeping; losing host
    /// reverses the lobby's host-only UI. Vanilla only ever runs these on disconnect migrations, so
    /// a voluntary transfer has to drive them itself.
    /// </summary>
    private static void RefreshLocalHostRole(bool isHost)
    {
        if (isHost)
        {
            try
            {
                AmongUsClient.Instance.OnBecomeHost();
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Warning($"OnBecomeHost replay failed: {ex}");
            }
        }

        RefreshLobbyHostUi(isHost);
    }

    /// <summary>
    /// Aligns GameStartManager's one-time host/client panels with the new role. These are toggled
    /// once at startup instead of per-frame, so a mid-lobby transfer leaves them stale unless
    /// refreshed here. Anything missed degrades to the next vanilla UI refresh.
    /// </summary>
    private static void RefreshLobbyHostUi(bool isHost)
    {
        var startManager = DestroyableSingleton<GameStartManager>.Instance;
        if (startManager == null)
            return;

        startManager.HostInfoPanelButtons.SetActive(isHost);
        startManager.ClientInfoPanelButtons.SetActive(!isHost);
        startManager.HostPrivacyButtons.SetActive(isHost);
        startManager.ClientPrivacyValue.SetActive(!isHost);
        startManager.EditButton.gameObject.SetActive(isHost);
        startManager.HostViewButton.gameObject.SetActive(isHost);
        startManager.ClientViewButton.gameObject.SetActive(!isHost);
        startManager.StartButtonClient.gameObject.SetActive(!isHost);
        startManager.StartButtonGlyphContainer.SetActive(isHost);

        if (isHost)
        {
            try
            {
                startManager.DoHostSetup();
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Warning($"Lobby host setup replay failed: {ex}");
            }
        }
        else
        {
            // A demoted host must not keep the settings editor or its edit affordances open.
            startManager.RulesEditPanel.SetActive(false);
            startManager.CloseGameOptionsMenus();
        }

        startManager.LobbyInfoPane?.RefreshPane();
    }

    /// <summary>
    /// Repaints every host indicator that was computed once: the meeting icon plus each player's
    /// lobby host panel badge.
    /// </summary>
    private static void RefreshHostIndicators()
    {
        MeetingHudPatch.UpdateHostIcon();

        if (GameData.Instance == null)
            return;

        foreach (var playerInfo in GameData.Instance.AllPlayers)
        {
            try
            {
                playerInfo.UpdateHostPanelImage();
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Warning($"Host panel image refresh failed: {ex}");
            }
        }
    }

    /// <summary>
    /// Replays the embedded server's genuine host-migration message (RemovePlayer tagged with the
    /// HostInherit sentinel instead of a disconnected id) so every connected client — including
    /// unmodded LAN clients — applies the new HostId, then aligns the server's own HostId.
    /// </summary>
    private static void TryLocalServerMigration(int targetClientId)
    {
        try
        {
            var server = DestroyableSingleton<InnerNetServer>.Instance;
            if (server == null)
                return;

            if (!TrySetServerHostId(server, targetClientId))
                return;

            var broadcast = typeof(InnerNetServer).GetMethod(
                "Broadcast", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (broadcast == null)
                return;

            // Identical wire format to InnerNetServer.ClientDisconnect's migration broadcast,
            // with the HostInherit sentinel standing in for the disconnected id so nobody is removed.
            var msg = MessageWriter.Get(SendOption.Reliable);
            msg.StartMessage((byte)Tags.RemovePlayer);
            msg.Write(InnerNetServer.LocalGameId);
            msg.Write(InnerNetClient.HostInherit);
            msg.Write(targetClientId);
            msg.Write(0);
            msg.EndMessage();

            broadcast.Invoke(server, [msg, null]);
            msg.Recycle();
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Warning($"Local host-migration broadcast failed: {ex}");
        }
    }

    /// <summary>
    /// Writes the private HostId field of the embedded server. Il2CppInterop exposes il2cpp fields
    /// either as plain fields, value-field wrappers or accessor properties, so all three shapes are handled.
    /// </summary>
    private static bool TrySetServerHostId(InnerNetServer server, int targetClientId)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        var field = typeof(InnerNetServer).GetField("HostId", flags);
        if (field != null)
        {
            if (field.FieldType == typeof(int))
            {
                field.SetValue(server, targetClientId);
                return true;
            }

            var wrapper = field.GetValue(server);
            var valueProperty = wrapper?.GetType().GetProperty("Value");
            if (valueProperty != null)
            {
                valueProperty.SetValue(wrapper, targetClientId);
                return true;
            }
        }

        var property = typeof(InnerNetServer).GetProperty("HostId", flags);
        if (property is { CanWrite: true })
        {
            property.SetValue(server, targetClientId);
            return true;
        }

        return false;
    }

    internal static string Describe(HostTransferDenial denial) => denial switch
    {
        HostTransferDenial.NotHost => TranslationStrings.HostTransfer_Denied_NotHost.LocalizedString,
        HostTransferDenial.NotInGame => TranslationStrings.HostTransfer_Denied_NotInGame.LocalizedString,
        HostTransferDenial.GameEnded => TranslationStrings.HostTransfer_Denied_GameEnded.LocalizedString,
        HostTransferDenial.GameStarting => TranslationStrings.HostTransfer_Denied_GameStarting.LocalizedString,
        HostTransferDenial.NotInLobby => TranslationStrings.HostTransfer_Denied_NotInLobby.LocalizedString,
        HostTransferDenial.TargetMissing => TranslationStrings.HostTransfer_Denied_TargetMissing.LocalizedString,
        HostTransferDenial.TargetIsDummy => TranslationStrings.HostTransfer_Denied_TargetIsDummy.LocalizedString,
        HostTransferDenial.TargetIsLocal => TranslationStrings.HostTransfer_Denied_TargetIsSelf.LocalizedString,
        HostTransferDenial.TargetIsHost => TranslationStrings.HostTransfer_Denied_TargetIsHost.LocalizedString,
        HostTransferDenial.TargetNotReady => TranslationStrings.HostTransfer_Denied_TargetNotReady.LocalizedString,
        HostTransferDenial.NoEligibleTarget => TranslationStrings.HostTransfer_Denied_NoEligibleTarget.LocalizedString,
        _ => string.Empty,
    };
}
