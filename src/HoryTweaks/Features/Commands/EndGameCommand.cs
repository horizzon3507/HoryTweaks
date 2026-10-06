
using HoryTweaks.Generated;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Game;

namespace HoryTweaks.Features.Commands;

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
