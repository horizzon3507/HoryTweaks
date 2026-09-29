using BetterAmongUs.Attributes;
using BetterAmongUs.Commands.Arguments;
using BetterAmongUs.Generated;
using BetterAmongUs.Utilities;
using System.Text;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class PlayerInfoCommand : BaseCommand
{
    internal override string Name => "player";
    internal override string Description => TranslationStrings.Command_PlayerInfo_Description.LocalizedString;

    public PlayerInfoCommand()
    {
        _playerArgument = new PlayerInfoArgument(this);
        Arguments = [_playerArgument];
    }
    private readonly PlayerInfoArgument _playerArgument;

    internal override void Run()
    {
        if (!_playerArgument.TryParse(out var playerData))
            return;

        if (playerData == null)
            return;

        StringBuilder sb = new();
        var hexColor = Utils.Color32ToHex(Palette.PlayerColors[playerData.DefaultOutfit.ColorId]);
        var format1 = "┌ •";
        var format2 = "├ •";
        var format3 = "└ •";
        sb.Append($"<size=150%><color={hexColor}><b>{playerData.PlayerName}</color> {TranslationStrings.Command_PlayerInfo_Info.LocalizedString}:</b></size>\n");
        sb.Append($"{format1} <color=#c1c1c1>{TranslationStrings.Command_PlayerInfo_ID.LocalizedString}: {playerData.PlayerId}</color>\n");
        sb.Append($"{format2} <color=#c1c1c1>HashPUID: {Utils.GetHashStr($"{playerData.Puid}")}</color>\n");
        var client = Utils.ClientFromClientId(playerData.ClientId);
        if (client != null)
        {
            sb.Append($"{format2} <color=#c1c1c1>{TranslationStrings.Command_PlayerInfo_Platform.LocalizedString}: {Utils.GetPlatformName(client.PlatformData.Platform)}</color>\n");
        }
        sb.Append($"{format3} <color=#c1c1c1>{TranslationStrings.Command_PlayerInfo_FriendCode.LocalizedString}: {playerData.FriendCode}</color>");
        CommandResultText(sb.ToString());
    }
}
