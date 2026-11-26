using AmongUs.InnerNet.GameDataMessages;
using BepInEx.Unity.IL2CPP.Utils;
using HoryTweaks.Core.Networking;
using HoryTweaks.Features.GameOptions;
using HoryTweaks.Game;
using HoryTweaks.Game.Players;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Networking.Rpc;
using HoryTweaks.Structs;
using HoryTweaks.Utilities;
using HarmonyLib;
using Hazel;
using InnerNet;
using System.Collections;
using UnityEngine;

namespace HoryTweaks.Networking;

/// <summary>
/// Manages network communication, low-allocation GameData dispatch, and RPC handling.
/// </summary>
internal static class NetworkManager
{
    private const int MaxDeferredAttempts = 10;
    private const float InvalidTagLogCooldownSeconds = 5f;

    private static readonly bool[] KnownRpcCalls = BuildKnownRpcCallsTable();
    private static readonly SendErrorTracker SendErrorsTracker = new();
    private static float _lastInvalidTagLogTime = float.NegativeInfinity;

    internal static long FastPathPacketsDispatched { get; private set; }
    internal static long DeferredPacketsDispatched { get; private set; }
    internal static int AbsorbedTransientSendErrors => SendErrorsTracker.AbsorbedTransientErrors;

    /// <summary>
    /// Gets the InnerNetClient instance from AmongUsClient.
    /// </summary>
    internal static InnerNetClient InnerNetClient => AmongUsClient.Instance;

    /// <summary>
    /// Resets per-session network counters when connecting or disconnecting.
    /// </summary>
    internal static void ResetSession()
    {
        SendErrorsTracker.Reset();
        FastPathPacketsDispatched = 0;
        DeferredPacketsDispatched = 0;
        _lastInvalidTagLogTime = float.NegativeInfinity;
    }

    /// <summary>
    /// Formats packet-dispatch diagnostics for the /dump report.
    /// </summary>
    internal static string FormatDiagnosticSummary()
    {
        return $"fastPath={FastPathPacketsDispatched}, deferred={DeferredPacketsDispatched}, absorbedSendErrors={AbsorbedTransientSendErrors}";
    }

    /// <summary>
    /// Sends a message to the game server while avoiding unnecessary buffer copies for non-host
    /// clients and absorbing isolated transient send failures on unreliable packets.
    /// </summary>
    internal static void SendToServer(MessageWriter writer)
    {
        var client = InnerNetClient;
        if (client == null)
            return;

        // Only hosts run host-side RPC validation on outgoing packets; non-host clients
        // skip local RPC handling in BetterRpcRouter, so inspecting every outgoing movement
        // packet would only allocate buffers and burn CPU.
        if (client.InOnlineScene && GameState.IsHost && writer.Length > 0)
        {
            try
            {
                StreamlineMessage(writer, writer.SendOption);
            }
            catch (Exception ex)
            {
                BAUPlugin.Logger.Error(ex, "NetworkManager");
            }
        }

        if (client.connection != null)
        {
            SendErrors sendErrors = client.connection.Send(writer);
            if (sendErrors != SendErrors.None && !GameState.IsFreePlay)
            {
                if (!BAUConfigs.NetworkOptimization.Value)
                {
                    client.EnqueueDisconnect(DisconnectReasons.Error, "Failed to send message: " + sendErrors.ToString());
                    return;
                }

                var errorKind = MapSendError(sendErrors);
                bool isReliable = writer.SendOption == SendOption.Reliable;
                var action = SendErrorsTracker.Evaluate(errorKind, isReliable, Time.unscaledTimeAsDouble, out bool shouldWarn);

                if (action == SendErrorAction.Disconnect)
                {
                    client.EnqueueDisconnect(DisconnectReasons.Error, "Failed to send message: " + sendErrors.ToString());
                }
                else if (shouldWarn)
                {
                    BAUPlugin.Logger.Warning(
                        $"Transient {(isReliable ? "reliable" : "unreliable")} send error ({sendErrors}) absorbed without disconnecting.",
                        "NetworkManager");
                }
            }
            else if (sendErrors == SendErrors.None)
            {
                SendErrorsTracker.Evaluate(SendErrorKind.None, false, Time.unscaledTimeAsDouble, out _);
            }
        }
        else
        {
            client.EnqueueDisconnect(DisconnectReasons.Custom, "InnerNetClient.connection is null");
        }
    }

    /// <summary>
    /// Inspects outgoing host packets for RPC messages without leaking pooled MessageReaders.
    /// </summary>
    internal static void StreamlineMessage(MessageWriter writer, SendOption sendOption)
    {
        var client = InnerNetClient;
        if (client == null || !client.InOnlineScene)
            return;

        MessageReader rootReader = writer.ToReader();
        try
        {
            while (rootReader.Position < rootReader.Length)
            {
                MessageReader subReader = rootReader.ReadMessage();
                try
                {
                    if (subReader.Tag == 5 || subReader.Tag == 6)
                    {
                        ReadData(subReader, sendOption);
                    }
                }
                finally
                {
                    subReader.Recycle();
                }
            }
        }
        finally
        {
            rootReader.Recycle();
        }
    }

    /// <summary>
    /// Reads outgoing GameData/GameDataTo payloads using zero-copy slices and guaranteed recycling.
    /// </summary>
    internal static void ReadData(MessageReader reader, SendOption sendOption)
    {
        byte typeFlag = reader.Tag;
        _ = reader.ReadInt32(); // GameId
        int clientId = -1;

        if (typeFlag == 6)
        {
            clientId = reader.ReadPackedInt32();
        }

        while (reader.Position < reader.Length)
        {
            MessageReader dataReader = reader.ReadMessage();
            try
            {
                if (dataReader.Tag == (byte)GameDataTypes.RpcFlag)
                {
                    var data = ReadRpc(dataReader, typeFlag, clientId);
                    if (data.Sender != null)
                    {
                        HandleInnerNetObject(data.Sender, (byte)data.CalledRpc, data.Reader);
                    }
                }
            }
            finally
            {
                dataReader.Recycle();
            }
        }
    }

    /// <summary>
    /// Reads RPC data from a MessageReader.
    /// </summary>
    internal static RPCData ReadRpc(MessageReader reader, byte flag, int targetId)
    {
        uint netId = reader.ReadPackedUInt32();
        byte calledId = reader.ReadByte();

        return new RPCData(
            AmongUsClient.Instance.FindObjectByNetId<InnerNetObject>(netId),
            (SendOption)flag,
            targetId,
            (RpcCalls)calledId,
            reader);
    }

    /// <summary>
    /// Handles incoming GameData sub-messages synchronously on the fast path (zero coroutines,
    /// zero buffer copies) whenever the target object is already spawned, falling back to a
    /// deferred coroutine only for spawns or packets that arrive ahead of spawn.
    /// </summary>
    public static void HandleGameData(MessageReader parentReader)
    {
        var client = InnerNetClient;
        if (client == null)
        {
            parentReader.Recycle();
            return;
        }

        try
        {
            bool hasParentTagHandlers = RpcHandlerRegistry.HasGameDataTagHandler(parentReader.Tag);

            while (parentReader.Position < parentReader.Length)
            {
                int subMessageStart = parentReader.Position;
                MessageReader messageReader = parentReader.ReadMessage();
                int currentMessageNumber = client.msgNum++;

                try
                {
                    if (hasParentTagHandlers)
                    {
                        MessageReader tagView = MessageReader.Get(messageReader);
                        try
                        {
                            RPCHandler.HandleRPC(parentReader.Tag, null, tagView, HandlerFlag.HandleGameDataTag);
                        }
                        finally
                        {
                            tagView.Recycle();
                        }
                    }

                    DispatchGameDataMessage(client, parentReader, subMessageStart, messageReader, currentMessageNumber);
                }
                catch (Exception ex)
                {
                    BAUPlugin.Logger.Error(ex, "NetworkManager");
                }
                finally
                {
                    messageReader.Recycle();
                }
            }
        }
        finally
        {
            parentReader.Recycle();
        }
    }

    private static void DispatchGameDataMessage(
        InnerNetClient client,
        MessageReader parentReader,
        int subMessageStart,
        MessageReader reader,
        int msgNum)
    {
        reader.Position = 0;

        switch ((GameDataTypes)reader.Tag)
        {
            case GameDataTypes.DataFlag:
                HandleObjectDeserializationFast(client, parentReader, subMessageStart, reader, msgNum);
                break;

            case GameDataTypes.RpcFlag:
                HandleRpcCallFast(client, parentReader, subMessageStart, reader, msgNum);
                break;

            case GameDataTypes.SpawnFlag:
                DeferredPacketsDispatched++;
                client.StartCoroutine(client.CoHandleSpawn(CloneSubMessage(parentReader, subMessageStart)));
                break;

            case GameDataTypes.DespawnFlag:
                FastPathPacketsDispatched++;
                HandleObjectDestruction(client, reader);
                break;

            case GameDataTypes.ReadyFlag:
                FastPathPacketsDispatched++;
                HandleClientReady(client, reader);
                break;

            case GameDataTypes.SceneChangeFlag:
                FastPathPacketsDispatched++;
                HandleSceneChange(client, reader);
                break;

            case GameDataTypes.XboxDeclareXuid:
                FastPathPacketsDispatched++;
                break;

            default:
                HandleInvalidTag(reader);
                break;
        }
    }

    private static void HandleObjectDeserializationFast(
        InnerNetClient client,
        MessageReader parentReader,
        int subMessageStart,
        MessageReader reader,
        int msgNum)
    {
        uint netId = reader.ReadPackedUInt32();

        if (client.allObjects.AllObjectsFast.TryGetValue(netId, out InnerNetObject innerNetObject))
        {
            FastPathPacketsDispatched++;
            innerNetObject.Deserialize(reader, false);
            return;
        }

        if (client.DestroyedObjects.Contains(netId))
        {
            FastPathPacketsDispatched++;
            return;
        }

        // Rare slow path: packet arrived before object spawn — clone buffer and retry asynchronously.
        DeferredPacketsDispatched++;
        Debug.LogWarning($"Stored data for {netId}");
        client.StartCoroutine(HandleObjectDeserializationDeferred(CloneSubMessage(parentReader, subMessageStart), msgNum));
    }

    private static IEnumerator HandleObjectDeserializationDeferred(MessageReader reader, int msgNum)
    {
        int attemptCount = 1;
        try
        {
            while (attemptCount <= MaxDeferredAttempts)
            {
                yield return Effects.Wait(0.1f);

                var client = InnerNetClient;
                if (client == null)
                    yield break;

                reader.Position = 0;
                uint netId = reader.ReadPackedUInt32();

                if (client.allObjects.AllObjectsFast.TryGetValue(netId, out InnerNetObject innerNetObject))
                {
                    innerNetObject.Deserialize(reader, false);
                    yield break;
                }

                if (client.DestroyedObjects.Contains(netId))
                {
                    yield break;
                }

                attemptCount++;
            }
        }
        finally
        {
            reader.Recycle();
        }
    }

    private static void HandleRpcCallFast(
        InnerNetClient client,
        MessageReader parentReader,
        int subMessageStart,
        MessageReader reader,
        int msgNum)
    {
        uint netId = reader.ReadPackedUInt32();
        byte rpcCall = reader.ReadByte();

        if (client.allObjects.AllObjectsFast.TryGetValue(netId, out InnerNetObject innerNetObject))
        {
            FastPathPacketsDispatched++;
            ExecuteRpc(innerNetObject, rpcCall, reader);
            return;
        }

        if (netId == uint.MaxValue || client.DestroyedObjects.Contains(netId))
        {
            FastPathPacketsDispatched++;
            return;
        }

        // Rare slow path: RPC arrived before target netId spawned.
        DeferredPacketsDispatched++;
        Debug.LogWarning($"Stored Msg {msgNum} RPC {(RpcCalls)rpcCall} for {netId}");
        client.StartCoroutine(HandleRpcCallDeferred(CloneSubMessage(parentReader, subMessageStart), msgNum));
    }

    private static IEnumerator HandleRpcCallDeferred(MessageReader reader, int msgNum)
    {
        int attemptCount = 1;
        try
        {
            while (attemptCount <= MaxDeferredAttempts)
            {
                yield return Effects.Wait(0.1f);

                var client = InnerNetClient;
                if (client == null)
                    yield break;

                reader.Position = 0;
                uint netId = reader.ReadPackedUInt32();
                byte rpcCall = reader.ReadByte();

                if (client.allObjects.AllObjectsFast.TryGetValue(netId, out InnerNetObject innerNetObject))
                {
                    ExecuteRpc(innerNetObject, rpcCall, reader);
                    yield break;
                }

                if (netId == uint.MaxValue || client.DestroyedObjects.Contains(netId))
                {
                    yield break;
                }

                attemptCount++;
            }
        }
        finally
        {
            reader.Recycle();
        }
    }

    private static void ExecuteRpc(InnerNetObject innerNetObject, byte rpcCall, MessageReader reader)
    {
        if (!HandleInnerNetObject(innerNetObject, rpcCall, reader))
            return;

        if (innerNetObject is PlayerPhysics playerPhysics && rpcCall == (byte)RPC.CUSTOM_RPC_CALL)
        {
            if (RPC.HandleCustomRPCPacked(playerPhysics.myPlayer, reader))
                return;
        }

        if (KnownRpcCalls[rpcCall])
        {
            innerNetObject.HandleRpc(rpcCall, reader);
        }
        else if (innerNetObject is PlayerControl player2)
        {
            RPC.HandleCustomRPCLegacy(player2, rpcCall, reader);
        }
    }

    private static void HandleObjectDestruction(InnerNetClient client, MessageReader reader)
    {
        uint netId = reader.ReadPackedUInt32();
        client.DestroyedObjects.Add(netId);

        InnerNetObject innerNetObject = client.FindObjectByNetId<InnerNetObject>(netId);
        if (innerNetObject != null && !innerNetObject.AmOwner)
        {
            client.RemoveNetObject(innerNetObject);
            innerNetObject.gameObject.DestroyObj();
        }
    }

    private static void HandleClientReady(InnerNetClient client, MessageReader reader)
    {
        ClientData clientData = client.FindClientById(reader.ReadPackedInt32());
        if (clientData != null)
        {
            clientData.IsReady = true;
        }
    }

    private static void HandleSceneChange(InnerNetClient client, MessageReader reader)
    {
        int clientId = reader.ReadPackedInt32();
        ClientData clientData = client.FindClientById(clientId);
        string sceneName = reader.ReadString();

        if (clientData != null && !string.IsNullOrWhiteSpace(sceneName))
        {
            client.StartCoroutine(client.CoOnPlayerChangedScene(clientData, sceneName));
        }
        else
        {
            Debug.Log($"Couldn't find client {clientId} to change scene to {sceneName}");
        }
    }

    private static void HandleInvalidTag(MessageReader reader)
    {
        float now = Time.unscaledTime;
        if (now - _lastInvalidTagLogTime < InvalidTagLogCooldownSeconds)
            return;

        _lastInvalidTagLogTime = now;
        BAUPlugin.Logger.Warning(
            $"Bad tag {reader.Tag} at {reader.Offset}+{reader.Position}={reader.Length}",
            "NetworkManager");
    }

    private static MessageReader CloneSubMessage(MessageReader parentReader, int subMessageStart)
    {
        int savedEnd = parentReader.Position;
        parentReader.Position = subMessageStart;
        MessageReader ownedCopy = parentReader.ReadMessageAsNewBuffer();
        parentReader.Position = savedEnd;
        return ownedCopy;
    }

    private static bool HandleInnerNetObject(InnerNetObject netObj, byte callId, MessageReader reader)
    {
        if (netObj is PlayerControl player)
        {
            return PlayerRpc(player, callId, reader);
        }

        if (netObj is PlayerPhysics physics)
        {
            return physics.myPlayer == null || PlayerRpc(physics.myPlayer, callId, reader);
        }

        if (netObj is CustomNetworkTransform networkTransform)
        {
            return networkTransform.myPlayer == null || PlayerRpc(networkTransform.myPlayer, callId, reader);
        }

        return true;
    }

    private static bool PlayerRpc(PlayerControl player, byte callId, MessageReader reader)
    {
        var extendedData = player.ExtendedData();
        if (extendedData != null && BetterGameSettings.RpcRateLimiting.GetBool())
        {
            extendedData.ActivityInfo.RPCSentPS++;
            if (extendedData.ActivityInfo.RPCSentPS >= BetterGameSettings.RpcRateLimit.GetInt())
            {
                return false;
            }
        }

        if (GameState.IsHost && RpcHandlerRegistry.GetCallIdHandlers(callId) != null)
        {
            var tempReader = MessageReader.Get(reader);
            try
            {
                if (!RPCHandler.HandleRPC(callId, player, tempReader, HandlerFlag.BetterHost))
                {
                    return false;
                }
            }
            finally
            {
                tempReader.Recycle();
            }
        }

        BetterRpcRouter.HandleRPC(player, callId, reader);
        return true;
    }

    private static SendErrorKind MapSendError(SendErrors error) => error switch
    {
        SendErrors.None => SendErrorKind.None,
        SendErrors.Disconnected => SendErrorKind.Disconnected,
        _ => SendErrorKind.Unknown
    };

    private static bool[] BuildKnownRpcCallsTable()
    {
        var table = new bool[256];
        foreach (RpcCalls call in Enum.GetValues<RpcCalls>())
        {
            byte value = (byte)call;
            table[value] = true;
        }

        return table;
    }

    [HarmonyPatch]
    internal static class MessageReaderUpdateSystemPatch
    {
        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(MessageReader))]
        [HarmonyPrefix]
        internal static bool ShipStatus_UpdateSystem_Prefix(SystemTypes systemType, PlayerControl player, MessageReader msgReader)
        {
            if (BetterGameSettings.RpcRateLimiting.GetBool())
            {
                var ext = player != null ? player.ExtendedData() : null;
                if (ext != null && ext.ActivityInfo != null)
                {
                    ext.ActivityInfo.RPCSentPS++;
                    if (ext.ActivityInfo.RPCSentPS >= BetterGameSettings.RpcRateLimit.GetInt())
                    {
                        return false;
                    }
                }
            }

            return BetterRpcRouter.RpcUpdateSystemCheck(player, systemType, msgReader);
        }
    }
}
