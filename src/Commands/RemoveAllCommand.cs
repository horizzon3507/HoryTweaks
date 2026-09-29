using BetterAmongUs.Data;
using BetterAmongUs.Attributes;
using BetterAmongUs.Generated;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class RemoveAllCommand : BaseCommand
{
    internal override string Name => "removeall";
    internal override string Description => TranslationStrings.Command_RemoveAll_Description.LocalizedString;
    internal override void Run()
    {
        BetterDataManager.ClearCheatData();
        CommandResultText(TranslationStrings.Command_RemoveAll_Success.LocalizedString);
    }
}
