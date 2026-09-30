using BetterAmongUs.Attributes;
using BetterAmongUs.Data;
using BetterAmongUs.Utilities;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using Hazel;

namespace BetterAmongUs.Modules.Rpc.RPCHandlers;

[RegisterRPCHandler]
internal sealed class SendChatHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.SendChat;

    internal override void Handle(PlayerControl? sender, MessageReader reader)
    {
        var text = reader.ReadString();

        if (BetterGameSettings.UseBanChatList.GetBool() && (!BetterGameSettings.UseBanChatListOnlyLobby.GetBool() || GameState.IsLobby))
        {
            if (TextFileHandler.CompareStringRegexMatches(BetterDataManager.Files.banChatListFilePath, text))
            {
                var ban = BetterGameSettings.UseBanChatListBan.GetBool();
                sender.Kick(ban, $"has been {(ban ? "banned" : "kicked")} due to\nchat message matching a banned pattern!");
            }
        }
    }
}
