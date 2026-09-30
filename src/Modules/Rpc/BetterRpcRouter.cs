using BetterAmongUs.Attributes;
using BetterAmongUs.Enums;
using BetterAmongUs.Modules.Rpc.RPCHandlers.NetObjectHandlers;
using BetterAmongUs.Utilities;
using Hazel;

namespace BetterAmongUs.Modules.Rpc;

/// <summary>
/// Provides the RPC routing used by BetterAmongUs handlers.
/// </summary>
internal static class BetterRpcRouter
{
    /// <summary>
    /// Handles RPCs received from players.
    /// </summary>
    /// <param name="player">The player who sent the RPC.</param>
    /// <param name="callId">The RPC call ID.</param>
    /// <param name="oldReader">The MessageReader containing RPC data.</param>
    internal static void HandleRPC(PlayerControl player, byte callId, MessageReader oldReader)
    {
        if (player == null || player.Data == null || player.IsLocalPlayer())
            return;

        MessageReader reader = MessageReader.Get(oldReader);
        RPCHandler.HandleRPC(callId, player, reader, HandlerFlag.Handle);
        reader.Recycle();
    }

    /// <summary>
    /// Applies host-side validation when systems are updated.
    /// </summary>
    /// <param name="player">The player attempting the sabotage.</param>
    /// <param name="systemType">The system type being updated.</param>
    /// <param name="oldReader">The MessageReader containing system update data.</param>
    /// <returns>True if the sabotage should be allowed, false otherwise.</returns>
    internal static bool RpcUpdateSystemCheck(PlayerControl player, SystemTypes systemType, MessageReader oldReader)
    {
        MessageReader reader = MessageReader.Get(oldReader);
        RegisterRPCHandlerAttribute.GetInstance<UpdateSystemHandler>().CatchedSystemType = systemType;
        if (!RPCHandler.HandleRPC((byte)RpcCalls.UpdateSystem, player, reader, HandlerFlag.BetterHost))
        {
            reader.Recycle();
            return false;
        }
        reader.Recycle();

        return true;
    }
}