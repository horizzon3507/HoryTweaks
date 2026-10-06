
using Semver;
using System.Text.Json.Serialization;
using HoryTweaks.Core.Updates;

namespace HoryTweaks.Remote.Updates;

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
    /// Gets or sets the full release package links per store variant, when the feed
    /// advertises them. Absent on feeds generated before package advertising existed.
    /// </summary>
    [JsonPropertyName("packages")]
    public UpdatePackageLinks? Packages { get; set; }

    /// <summary>
    /// Gets or sets the URL of the release's SHA256SUMS.txt checksum manifest.
    /// </summary>
    [JsonPropertyName("sha256Link")]
    public string Sha256Link { get; set; } = string.Empty;

    /// <summary>
    /// Determines if this update is newer than the currently installed version.
    /// </summary>
    /// <returns>True if the update is newer, false otherwise.</returns>
    internal bool IsNewUpdate()
    {
        try
        {
            return UpdateVersionCheck.IsNewerThanInstalled(Valid, Version, BAUPlugin.ModInfo.SemVersion);
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