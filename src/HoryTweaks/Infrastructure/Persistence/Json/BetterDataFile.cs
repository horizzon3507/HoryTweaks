using HoryTweaks.Structs;
using System.Text.Json.Serialization;

namespace HoryTweaks.Infrastructure.Persistence.Json;

/// <summary>
/// Represents the main data file for BetterAmongUs, containing outfit presets.
/// </summary>
internal sealed class BetterDataFile : AbstractJsonFile
{
    /// <summary>
    /// Gets the file path for the BetterAmongUs data file.
    /// </summary>
    internal override string FilePath => BetterDataManager.Files.dataFilePath;

    /// <summary>
    /// Loads the data file and performs post-load processing.
    /// </summary>
    /// <returns>True if loading was successful, false otherwise.</returns>
    protected override bool Load()
    {
        var success = base.Load();
        if (success)
        {
            SelectedOutfitPreset = Math.Clamp(SelectedOutfitPreset, 0, 5);
        }
        return success;
    }

    /// <summary>
    /// Saves the data file.
    /// </summary>
    /// <returns>True if saving was successful, false otherwise.</returns>
    internal override bool Save()
    {
        return base.Save();
    }

    /// <summary>
    /// Gets or sets the index of the currently selected outfit preset.
    /// </summary>
    [JsonPropertyName("selectedOutfitPreset")]
    public int SelectedOutfitPreset { get; set; } = 0;

    /// <summary>
    /// Gets or sets the collection of outfit presets.
    /// </summary>
    [JsonPropertyName("outfitData")]
    public HashSet<OutfitData> OutfitData { get; set; } = [new(), new(), new(), new(), new(), new()];

}