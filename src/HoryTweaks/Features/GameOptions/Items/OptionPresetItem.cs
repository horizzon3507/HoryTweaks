

using BetterAmongUs.Generated;
using BetterAmongUs.Infrastructure.Persistence.Json;
using BetterAmongUs.Modules.OptionItems;
using BetterAmongUs.Core.Presets;
using BetterAmongUs.Infrastructure.Configuration;
using BetterAmongUs.Infrastructure.Persistence;

namespace BetterAmongUs.Features.GameOptions.Items;

/// <summary>
/// Represents a preset option item that set the settings preset.
/// </summary>
internal sealed class OptionPresetItem : OptionStringItem
{
    internal override bool CanLoad => false;

    /// <summary>
    /// Gets the name the player gave the preset stored in the given settings file, if any.
    /// </summary>
    internal static string? GetCustomName(BetterGameSettingsFile file)
    {
        file.Settings.TryGetValue(PresetNameHelper.SettingKey, out var raw);
        return PresetNameHelper.Normalize(raw as string);
    }

    /// <summary>
    /// Gets the display name of the current preset: the custom name when set, otherwise a localized slot name.
    /// </summary>
    internal static string GetDisplayName(int preset)
    {
        string? custom = GetCustomName(BetterDataManager.Files.BetterGameSettingsFile);
        return custom ?? TranslationStrings.Setting_Preset.Format((preset + 1).ToString());
    }

    /// <summary>
    /// Stores a custom name for the current preset. Passing an empty name reverts to the slot name.
    /// </summary>
    internal static void SetCustomName(string? name)
    {
        var file = BetterDataManager.Files.BetterGameSettingsFile;
        string? normalized = PresetNameHelper.Normalize(name);
        if (normalized == null)
            file.Settings.Remove(PresetNameHelper.SettingKey);
        else
            file.Settings[PresetNameHelper.SettingKey] = normalized;
        file.Save();
    }

    /// <summary>
    /// Encodes the current preset (persisted option values and custom name) as a share code.
    /// </summary>
    internal static string Export()
    {
        var values = PersistedOptions
            .Select(opt => new KeyValuePair<string, object?>(opt.SettingKey, opt.GetBoxedValue()))
            .ToList();

        string? custom = GetCustomName(BetterDataManager.Files.BetterGameSettingsFile);
        if (custom != null)
            values.Add(new(PresetNameHelper.SettingKey, custom));

        return PresetShareCodec.Encode(values);
    }

    /// <summary>
    /// Applies a decoded share code to the current preset. Unknown keys and values of the wrong type are ignored.
    /// </summary>
    /// <returns>The number of option values that were applied.</returns>
    internal static int Import(Dictionary<string, object?> decoded)
    {
        var file = BetterDataManager.Files.BetterGameSettingsFile;
        int applied = 0;
        foreach (var opt in PersistedOptions)
        {
            if (!decoded.TryGetValue(opt.SettingKey, out var raw))
                continue;

            object? value = opt.NormalizeImportValue(raw);
            if (value == null)
                continue;

            file.Settings[opt.SettingKey] = value;
            applied++;
        }

        if (decoded.TryGetValue(PresetNameHelper.SettingKey, out var rawName)
            && PresetNameHelper.Normalize(rawName as string) is string name)
        {
            file.Settings[PresetNameHelper.SettingKey] = name;
        }

        file.Save();
        foreach (var opt in AllOptions)
        {
            opt.TryLoad(true);
            opt.UpdateVisuals(false);
        }

        GameSettingsPatch.BetterSettingsTab?.UpdateVisuals();
        return applied;
    }

    /// <summary>
    /// Creates a new preset item for the options menu.
    /// </summary>
    /// <returns>The created or reused <see cref="OptionPresetItem"/> instance.</returns>
    internal static OptionPresetItem Create()
    {
        if (GetOptionByTranslationName(TranslationStrings.Setting_Presets) is OptionPresetItem stringItem)
        {
            stringItem.CreateBehavior();
            return stringItem;
        }

        OptionPresetItem Item = new();
        AllOptions.Add(Item);
        Item.Tab = GameSettingsPatch.BetterSettingsTab;
        Item.TranslationName = TranslationStrings.Setting_Presets;
        Item.TranslatorStrings = Enumerable.Repeat(new TranslationStrings.TranslationString(string.Empty), 10).ToArray();
        Item.Range = new IntRange(0, 10);
        Item.DefaultValue = 0;
        Item.Value = BAUConfigs.SettingsPreset.Value;

        Item.CreateBehavior();
        return Item;
    }

    internal override void OnValueChange(int oldValue, int newValue)
    {
        BAUConfigs.SettingsPreset.Value = newValue;
        BetterDataManager.Files.BetterGameSettingsFile = new();
        BetterDataManager.Files.BetterGameSettingsFile.Init();
        foreach (var opt in AllOptions)
        {
            opt.TryLoad(true);
        }
        GameSettingsPatch.BetterSettingsTab.UpdateVisuals();
    }

    public sealed override string ValueAsString()
    {
        return GetDisplayName(Value);
    }
}
