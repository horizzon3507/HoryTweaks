using Semver;
using System.Text.Json.Serialization;

namespace BetterAmongUs.Network.Configs;

/// <summary>
/// Represents update data retrieved from the remote repository.
/// </summary>
[Serializable]
internal sealed class BAUUpdateData
{
    /// <summary>
    /// Gets or sets the download link for the updated DLL file.
    /// </summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; } = false;

    /// <summary>
    /// Gets or sets the download link for the updated DLL file.
    /// </summary>
    [JsonPropertyName("dllLink")]
    public string DllLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version string of the update.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Determines if this update is newer than the currently installed version.
    /// </summary>
    /// <returns>True if the update is newer, false otherwise.</returns>
    internal bool IsNewUpdate()
    {
        try
        {
            if (!Valid)
            {
                return false;
            }

            var updateVersion = SemVersion.Parse(Version);
            var modVersion = BAUPlugin.ModInfo.SemVersion;

            return updateVersion.ComparePrecedenceTo(modVersion) > 0;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error($"Update check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns a string representation of the update data.
    /// </summary>
    /// <returns>A formatted string containing version information.</returns>
    public override string ToString()
    {
        return $"{SemVersion.Parse(Version)}";
    }
}