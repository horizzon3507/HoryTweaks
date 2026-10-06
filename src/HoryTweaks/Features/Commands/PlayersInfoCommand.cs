
using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using System.Text;
using HoryTweaks.Plugin.Registration;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class PlayersInfoCommand : BaseCommand
{
    internal override string Name => "players";
    internal override string Description => TranslationStrings.Command_PlayersInfo_Description.LocalizedString;

    internal override void Run()
    {
        StringBuilder sb = new();
        foreach (PlayerControl player in BAUPlugin.AllPlayerControls.Where(player => !player.isDummy))
        {
            if (player == null || player.Data == null) continue;

            var hexColor = Utils.Color32ToHex(Palette.PlayerColors[player.CurrentOutfit.ColorId]);
            sb.Append($"<color={hexColor}><b>{player.Data.PlayerName}</color> {TranslationStrings.Command_PlayerInfo_Info.LocalizedString}:</b>\n");
            sb.Append($"<color=#c1c1c1>{player.Data.PlayerId}</color> - ");
            sb.Append($"<color=#c1c1c1>{Utils.GetHashStr($"{player.Data.Puid}")}</color> - ");
            sb.Append($"<color=#c1c1c1>{Utils.GetPlatformName(player)}</color> - ");
            sb.Append($"<color=#c1c1c1>{player.Data.FriendCode}</color>");
            sb.Append("\n\n");
        }
        CommandResultText(sb.ToString());
    }
}
