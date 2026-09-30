using BetterAmongUs.Data.Config;
using BetterAmongUs.Modules;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BetterAmongUs.Data.Json;

/// <summary>
/// Represents a compressed JSON file for storing game settings with GZIP compression.
/// </summary>
internal sealed class BetterGameSettingsFile : AbstractJsonFile
{
    /// <summary>
    /// Gets or initializes the custom file path override for the settings file.
    /// </summary>
    internal string? OverrideFilePath { get; init; }

    /// <summary>
    /// The current version identifier for the settings file format.
    /// </summary>
    internal const string SETTINGS_VERSION = SettingsFileCodec.SETTINGS_VERSION;

    /// <summary>
    /// The dictionary key used to store the settings file version.
    /// </summary>
    internal const string SETTINGS_VERSION_KEY = SettingsFileCodec.SETTINGS_VERSION_KEY;

    /// <summary>
    /// Gets the file path for the game settings file.
    /// </summary>
    internal override string FilePath => OverrideFilePath ?? BetterDataManager.Files.SettingsFilePath;

    /// <summary>
    /// Loads the settings file and converts JSON elements to their appropriate types.
    /// </summary>
    /// <returns>True if loading was successful, false otherwise.</returns>
    protected override bool Load()
    {
        var success = base.Load();
        if (success)
        {
            foreach (var kvp in Settings.ToArray())
            {
                if (kvp.Value is JsonElement jsonElement)
                {
                    try
                    {
                        Settings[kvp.Key] = SettingsFileCodec.ConvertElement(jsonElement);
                    }
                    catch (Exception ex)
                    {
                        BAUPlugin.Logger.Error($"Failed to convert JSON element for key {kvp.Key}: {ex.Message}");
                    }
                }
            }

            // Validate settings file version
            Settings.TryGetValue(SETTINGS_VERSION_KEY, out var versionObject);
            string? storedVersion = versionObject?.ToString();
            if (SettingsFileCodec.RequiresMigration(storedVersion))
            {
                CreateBackup(storedVersion);
                Save();
            }
        }
        return success;
    }

    private void CreateBackup(string? storedVersion)
    {
        if (!File.Exists(FilePath))
            return;

        var backupPath = SettingsFileCodec.GetBackupPath(FilePath, storedVersion);
        if (!File.Exists(backupPath))
        {
            File.Copy(FilePath, backupPath);
        }
    }

    /// <summary>
    /// Saves the current settings to the file, ensuring the version key is included.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the save operation was successful;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal override bool Save()
    {
        Settings = SettingsFileCodec.PrepareForSave(Settings);
        return base.Save();
    }

    /// <summary>
    /// Writes JSON data to the file with GZIP compression and base64 encoding.
    /// </summary>
    /// <param name="json">The JSON string to write to the file.</param>
    protected override void WriteToFile(string json)
    {
        if (!BAUConfigs.CompressSettingFiles.Value)
        {
            base.WriteToFile(json);
            return;
        }

        File.WriteAllText(FilePath, SettingsFileCodec.Encode(json, IsFloatSetting));
    }

    private bool IsFloatSetting(string key)
    {
        return Settings.TryGetValue(key, out var originalValue) && originalValue is float;
    }

    /// <summary>
    /// Reads compressed JSON data from the file and decompresses it.
    /// </summary>
    /// <returns>The decompressed JSON string.</returns>
    protected override string ReadFromFile()
    {
        return SettingsFileCodec.Decode(File.ReadAllText(FilePath));
    }

    /// <summary>
    /// Gets the dictionary of game settings with integer keys and various value types.
    /// </summary>
    [JsonPropertyName(SettingsFileCodec.SETTINGS_PROPERTY)]
    public Dictionary<string, object?> Settings { get; set; } = [];
}