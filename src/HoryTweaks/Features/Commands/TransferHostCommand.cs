

using HoryTweaks.Generated;
using HoryTweaks.Features.Commands.Arguments;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Game;

namespace HoryTweaks.Features.Commands;

[RegisterCommand]
internal sealed class TransferHostCommand : BaseCommand
{
    internal override string Name => "transferhost";
    internal override string[] ShortNames => ["th"];
    internal override string Description => TranslationStrings.HostTransfer_Command_Description.LocalizedString;
    internal override bool CanRunCommand(out string reason)
    {
        if (!GameState.IsHost)
        {
            reason = TranslationStrings.Command_Error_HostOnly.LocalizedString;
            return false;
        }

        return base.CanRunCommand(out reason);
    }
    public TransferHostCommand()
    {
        _playerArgument = new PlayerArgument(this);
        Arguments = [_playerArgument];
    }
    private readonly PlayerArgument _playerArgument;

    internal override void Run()
    {
        if (!_playerArgument.TryParse(out var player))
            return;

        if (Features.HostTransfer.HostTransfer.Execute(player, out var message))
            CommandResultText(message);
        else
            CommandErrorText(message);
    }
}
