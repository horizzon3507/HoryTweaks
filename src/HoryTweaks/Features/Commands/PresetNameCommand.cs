

using BetterAmongUs.Generated;
using BetterAmongUs.Modules.OptionItems;
using BetterAmongUs.Features.Commands.Arguments;
using BetterAmongUs.Plugin.Registration;
using BetterAmongUs.Core.Presets;
using BetterAmongUs.Features.GameOptions.Items;
using BetterAmongUs.Infrastructure.Configuration;

namespace BetterAmongUs.Features.Commands;

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
