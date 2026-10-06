

using BetterAmongUs.Generated;

using BetterAmongUs.Utilities;
using BetterAmongUs.Features.Commands.Arguments;
using BetterAmongUs.Plugin.Registration;
using BetterAmongUs.Game;

namespace BetterAmongUs.Features.Commands;

[RegisterCommand]
internal sealed class KickCommand : BaseCommand
{
    internal override string Name => "kick";
    internal override string Description => TranslationStrings.Command_Kick_Description.LocalizedString;
    internal override bool CanRunCommand(out string reason)
    {
        if (!GameState.IsHost)
        {
            reason = TranslationStrings.Command_Error_HostOnly.LocalizedString;
            return false;
        }

        return base.CanRunCommand(out reason);
    }
    public KickCommand()
    {
        _playerArgument = new PlayerArgument(this);
        _boolArgument = new BoolArgument(this, "{ban}");
        Arguments = [_playerArgument, _boolArgument];
    }
    private readonly PlayerArgument _playerArgument;
    private readonly BoolArgument _boolArgument;

    internal override void Run()
    {
        if (!_playerArgument.TryParse(out var player))
            return;

        if (!_boolArgument.TryParse(out var ban))
            return;

        if (!player.IsHost())
        {
            player.Kick(ban);
        }
    }
}
