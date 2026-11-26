using Hazel;
using InnerNet;
using UnityEngine;
using HoryTweaks.Game.Players;

namespace HoryTweaks.Networking.Rpc;

/// <summary>
/// Abstract base class for handling RPC (Remote Procedure Call) messages.
/// </summary>
internal abstract class RPCHandler
{
    /// <summary>
    /// Gets the InnerNetClient instance.
    /// </summary>
    internal InnerNetClient innerNetClient => AmongUsClient.Instance;

    /// <summary>
    /// Gets the RPC call ID that this handler responds to.
    /// </summary>
    internal virtual byte CallId => byte.MaxValue;

    /// <summary>
    /// Gets the GameData tag that this handler responds to.
    /// </summary>
    internal virtual byte GameDataTag => byte.MaxValue;

    /// <summary>
    /// Gets or sets whether this handler should process messages from the local player.
    /// </summary>
    internal virtual bool LocalHandling { get; set; }

    /// <summary>
    /// Checks if two positions are within a specified range.
    /// </summary>
    internal static bool CheckRange(Vector2 pos1, Vector2 pos2, float range) => Vector2.Distance(pos1, pos2) <= range;

    /// <summary>
    /// Handles normal RPC messages.
    /// </summary>
    internal virtual void Handle(PlayerControl? sender, MessageReader reader) { }

    /// <summary>
    /// Handles GameData messages.
    /// </summary>
    internal virtual void HandleGameData(MessageReader reader) { }

    /// <summary>
    /// Handles RPC messages with BetterAmongUs-specific logic.
    /// </summary>
    internal virtual bool BetterHandle(PlayerControl? sender, MessageReader reader) => true;

    protected static PlayerControl? catchedSender;
    protected static HandlerFlag catchedHandlerFlag = HandlerFlag.Handle;

    /// <summary>
    /// Routes RPC messages in O(1) using pre-indexed handler tables and precomputed support-flag keys.
    /// </summary>
    internal static bool HandleRPC(byte calledId, PlayerControl? sender, MessageReader reader, HandlerFlag handlerFlag)
    {
        RpcHandlerEntry[]? entries = handlerFlag == HandlerFlag.HandleGameDataTag
            ? RpcHandlerRegistry.GetGameDataTagHandlers(calledId)
            : RpcHandlerRegistry.GetCallIdHandlers(calledId);

        if (entries == null || entries.Length == 0)
            return true;

        catchedSender = sender;
        catchedHandlerFlag = handlerFlag;
        bool cancel = false;

        if (handlerFlag == HandlerFlag.HandleGameDataTag)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                ref readonly var entry = ref entries[i];
                if (entry.IsDisabled(handlerFlag))
                {
                    break;
                }

                try
                {
                    entry.Handler.HandleGameData(reader);
                }
                catch (Exception ex)
                {
                    BAUPlugin.Logger.Error(ex);
                }
            }

            return true;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            ref readonly var entry = ref entries[i];
            if (entry.IsDisabled(handlerFlag))
            {
                break;
            }

            try
            {
                switch (handlerFlag)
                {
                    case HandlerFlag.Handle:
                        entry.Handler.Handle(sender, reader);
                        break;
                    case HandlerFlag.BetterHost:
                        cancel = !entry.Handler.BetterHandle(sender, reader);
                        break;
                }

                if (!cancel)
                    break;
            }
            catch
            {
            }
        }

        return !cancel;
    }

    /// <summary>
    /// Logs RPC information for debugging and host validation.
    /// </summary>
    internal void LogRpcInfo(string info, PlayerControl? player = null)
    {
        string name = Enum.GetName((RpcCalls)CallId) ?? Enum.GetName((CustomRPC)CallId) ?? $"Unregistered({CallId})";
        name = $"[{Enum.GetName(catchedHandlerFlag)}] > " + name;
        string? senderName = catchedSender != null ? catchedSender.ExtendedData()?.RealName : null;
        string? playerName = player != null ? player.ExtendedData()?.RealName : null;
        BAUPlugin.Logger.Log($"{senderName ?? playerName ?? string.Empty} {name}: {info}", "RPC");
    }
}
