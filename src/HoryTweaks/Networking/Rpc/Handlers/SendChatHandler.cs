

using HoryTweaks.Generated;
using HoryTweaks.Utilities;

using Hazel;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Features.GameOptions;
using HoryTweaks.Game;
using HoryTweaks.Infrastructure.Persistence;

namespace HoryTweaks.Networking.Rpc.Handlers;

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
                sender.Kick(ban, TranslationStrings.HostTools_BanChatListMessage.LocalizedString);
            }
        }
    }
}
