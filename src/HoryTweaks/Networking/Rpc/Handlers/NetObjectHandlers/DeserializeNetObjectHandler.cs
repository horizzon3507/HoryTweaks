using AmongUs.InnerNet.GameDataMessages;

using Hazel;
using InnerNet;
using HoryTweaks.Features.Meeting;
using HoryTweaks.Game;

namespace HoryTweaks.Networking.Rpc.Handlers.NetObjectHandlers;

internal sealed class DeserializeNetObjectHandler : RPCHandler
{
    internal override byte GameDataTag => (byte)GameDataTypes.DataFlag;

    internal override void HandleGameData(MessageReader reader)
    {
        uint netId = reader.ReadPackedUInt32();
        var innerNetObject = innerNetClient.FindObjectByNetId<InnerNetObject>(netId);
        if (innerNetObject == null)
            return;

        if (innerNetObject.TryCast<CustomNetworkTransform>() && (GameState.IsMeeting && MeetingHudPatch.timeOpen > 5))
        {
            var player = innerNetObject.Cast<CustomNetworkTransform>()?.myPlayer;
            if (player == null)
                return;

            LogRpcInfo("Player attempted to move during meeting", player);
        }
    }
}