using BetterAmongUs.Attributes;
using BetterAmongUs.Commands.Arguments;
using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules.OptionItems;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class PresetNameCommand : BaseCommand
{
    internal override string Name => "presetname";
    internal override string Description => TranslationStrings.Command_PresetName_Description.LocalizedString;

    internal PresetNameCommand()
    {
        _nameArgument = new StringArgument(this, "{name}");
        Arguments = [_nameArgument];
    }
    private readonly StringArgument _nameArgument;

    internal override void Run()
    {
        if (!_nameArgument.TryParse(out var name))
            return;

        // Chat arguments are single words, so underscores stand in for spaces.
        string? normalized = PresetNameHelper.Normalize(name.Replace('_', ' '));
        OptionPresetItem.SetCustomName(normalized);

        string slot = TranslationStrings.Setting_Preset.Format((BAUConfigs.SettingsPreset.Value + 1).ToString());
        CommandResultText(normalized == null
            ? TranslationStrings.Command_PresetName_Cleared.Format(slot)
            : TranslationStrings.Command_PresetName_Updated.Format(slot, normalized));
    }
}
