using BetterAmongUs.Attributes;
using BetterAmongUs.Commands.Arguments;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.HostTransfer;

namespace BetterAmongUs.Commands;

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

        if (HostTransfer.Execute(player, out var message))
            CommandResultText(message);
        else
            CommandErrorText(message);
    }
}
