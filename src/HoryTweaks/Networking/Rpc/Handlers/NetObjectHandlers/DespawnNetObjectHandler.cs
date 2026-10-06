using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;

namespace BetterAmongUs.Networking.Rpc.Handlers.NetObjectHandlers;

internal sealed class DespawnNetObjectHandler : RPCHandler
{
    internal override byte GameDataTag => (byte)GameDataTypes.DespawnFlag;

    internal override void HandleGameData(MessageReader reader)
    {
        // if (!GameState.IsHost) return;

        uint netId = reader.ReadPackedUInt32();
        var innerNetObject = innerNetClient.FindObjectByNetId<InnerNetObject>(netId);
        if (innerNetObject is PlayerControl player)
        {
            LogRpcInfo("Player attempted to despawn another player", player);
        }
    }
}