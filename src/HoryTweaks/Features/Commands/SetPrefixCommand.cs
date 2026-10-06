

using BetterAmongUs.Generated;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Features.Commands.Arguments;
using BetterAmongUs.Plugin.Registration;
using BetterAmongUs.Infrastructure.Configuration;

namespace BetterAmongUs.Features.Commands;

[RegisterCommand]
internal sealed class SetPrefixCommand : BaseCommand
{
    internal override string Name => "setprefix";
    internal override string Description => TranslationStrings.Command_SetPrefix_Description.LocalizedString;

    internal SetPrefixCommand()
    {
        _prefixArgument = new StringArgument(this, "{prefix}");
        Arguments = [_prefixArgument];
    }
    private readonly StringArgument _prefixArgument;

    internal override bool ShowCommand() =>
        !BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Force_BAU_Command_Prefix);

    internal override void Run()
    {
        var oldPrefix = BAUConfigs.CommandPrefix.Value;
        if (!_prefixArgument.TryParse(out var prefix))
            return;

        prefix = prefix.Length > 0 ? prefix[..1] : string.Empty;
        if (!string.IsNullOrEmpty(prefix))
        {
            BAUConfigs.CommandPrefix.Value = prefix;
            CommandResultText(TranslationStrings.Command_Prefix_Updated.Format(oldPrefix, prefix));
        }
        else
        {
            CommandErrorText(TranslationStrings.Command_Error_InvalidSyntax.LocalizedString);
        }
    }
}
