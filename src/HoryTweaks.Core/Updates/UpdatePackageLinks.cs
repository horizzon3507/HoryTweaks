using System.Text.Json.Serialization;

namespace HoryTweaks.Core.Updates;

/// <summary>
/// Full-package download links advertised by the update feed's <c>packages</c> object.
/// </summary>
[Serializable]
internal sealed class UpdatePackageLinks
{
    /// <summary>
    /// Gets or sets the URL of the HoryTweaks-Steam-Epic-MsStore release package.
    /// </summary>
    [JsonPropertyName("steamEpicMsStore")]
    public string SteamEpicMsStore { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the HoryTweaks-Itchio release package.
    /// </summary>
    [JsonPropertyName("itchio")]
    public string Itchio { get; set; } = string.Empty;
}
