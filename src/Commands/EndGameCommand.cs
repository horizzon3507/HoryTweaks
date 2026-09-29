using BetterAmongUs.Attributes;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class EndGameCommand : BaseCommand
{
    internal override string Name => "endgame";
    internal override string Description => TranslationStrings.Command_EndGame_Description.LocalizedString;
    internal override bool CanRunCommand(out string reason)
    {
        if (!GameState.IsHost)
        {
            reason = TranslationStrings.Command_Error_HostOnly.LocalizedString;
            return false;
        }

        if (!GameState.IsInGamePlay)
        {
            reason = TranslationStrings.Command_Error_GameplayOnly.LocalizedString;
            return false;
        }

        return base.CanRunCommand(out reason);
    }

    internal override void Run()
    {
        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
    }
}
