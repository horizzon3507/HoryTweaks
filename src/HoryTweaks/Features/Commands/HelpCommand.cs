
using BetterAmongUs.Generated;
using BetterAmongUs.Plugin.Registration;

namespace BetterAmongUs.Features.Commands;

[RegisterCommand]
internal sealed class HelpCommand : BaseCommand
{
    internal override string Name => "help";
    internal override string Description => TranslationStrings.Command_Help_Description.LocalizedString;
    internal override void Run()
    {
        CommandResultText(TranslationStrings.Command_Help_Body.Format(TranslationStrings.BetterAmongUs));
    }
}
