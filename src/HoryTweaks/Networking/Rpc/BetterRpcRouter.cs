using HoryTweaks.Utilities;
using Hazel;

namespace HoryTweaks.Networking.Rpc;

/// <summary>
/// Provides the RPC routing used by BetterAmongUs handlers.
/// </summary>
internal static class BetterRpcRouter
{
    /// <summary>
    /// Handles RPCs received from remote players without leaking pooled readers on exceptions.
    /// </summary>
    internal static void HandleRPC(PlayerControl player, byte callId, MessageReader oldReader)
    {
        if (player == null || player.Data == null || player.IsLocalPlayer())
            return;

        // Avoid copying a MessageReader if no handler listens for this RPC call ID.
        if (RpcHandlerRegistry.GetCallIdHandlers(callId) == null)
            return;

        MessageReader reader = MessageReader.Get(oldReader);
        try
        {
            RPCHandler.HandleRPC(callId, player, reader, HandlerFlag.Handle);
        }
        finally
        {
            reader.Recycle();
        }
    }

    /// <summary>
    /// Applies host-side validation when systems are updated.
    /// </summary>
    internal static bool RpcUpdateSystemCheck(PlayerControl player, SystemTypes systemType, MessageReader oldReader)
    {
        MessageReader reader = MessageReader.Get(oldReader);
        try
        {
            RpcHandlerRegistry.UpdateSystemHandler.CatchedSystemType = systemType;
            return RPCHandler.HandleRPC((byte)RpcCalls.UpdateSystem, player, reader, HandlerFlag.BetterHost);
        }
        finally
        {
            reader.Recycle();
        }
    }
}
