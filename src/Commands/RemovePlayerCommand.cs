using BetterAmongUs.Attributes;
using BetterAmongUs.Commands.Arguments;
using BetterAmongUs.Data;
using BetterAmongUs.Generated;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class RemovePlayerCommand : BaseCommand
{
    internal override string Name => "removeplayer";
    internal override string Description => TranslationStrings.Command_RemovePlayer_Description.LocalizedString;

    public RemovePlayerCommand()
    {
        _identifierArgument = new StringArgument(this, "{identifier}")
        {
            ArgSuggestions = () =>
                [.. BetterDataManager.Files.BetterDataFile.AllCheatData.SelectMany(info => new[] { info.HashPuid.Replace(' ', '_'), info.FriendCode.Replace(' ', '_'), info.PlayerName.Replace(' ', '_') })]
        };
        Arguments = [_identifierArgument];
    }
    private readonly StringArgument _identifierArgument;

    internal override void Run()
    {
        if (_identifierArgument.TryParse(out var identifierArgument))
        {
            if (BetterDataManager.RemovePlayer(identifierArgument) == true)
            {
                CommandResultText(TranslationStrings.Command_RemovePlayer_Success.Format(identifierArgument));
            }
            else
            {
                CommandErrorText(TranslationStrings.Command_RemovePlayer_NotFound.Format(identifierArgument));
            }
        }
    }
}
