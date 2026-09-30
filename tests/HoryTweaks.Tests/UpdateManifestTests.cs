using BetterAmongUs.Network.Configs;
using Semver;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace HoryTweaks.Tests;

/// <summary>
/// Guards the committed update feeds against advertising a build that the plugin cannot install.
/// </summary>
public class UpdateManifestTests
{
    private const string DllLinkFormat = "https://github.com/horizzon3507/HoryTweaks/releases/download/v{0}/HoryTweaks.dll";

    private static readonly JsonDocumentOptions ManifestOptions = new() { AllowTrailingCommas = true };

    private static JsonElement ReadManifest(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(RepositoryPaths.Api, fileName));
        return JsonDocument.Parse(json, ManifestOptions).RootElement;
    }

    private static string ReadSourceVersionNumber()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryPaths.Src, "BAUPlugin.ModInfo.cs"));
        var match = Regex.Match(source, @"VERSION_NUMBER\s*=\s*""(?<version>[^""]+)""");
        Assert.True(match.Success, "VERSION_NUMBER was not found in BAUPlugin.ModInfo.cs.");
        return match.Groups["version"].Value;
    }

    [Fact]
    public void Current_feed_has_the_expected_shape()
    {
        var manifest = ReadManifest("update-V2.json");

        Assert.Equal(JsonValueKind.True, manifest.GetProperty("valid").ValueKind);
        var version = manifest.GetProperty("version").GetString();
        Assert.True(SemVersion.TryParse(version, SemVersionStyles.Strict, out _), $"'{version}' is not a strict semantic version.");
        Assert.Equal(string.Format(DllLinkFormat, version), manifest.GetProperty("dllLink").GetString());
    }

    [Fact]
    public void Legacy_feed_matches_the_current_feed()
    {
        var current = ReadManifest("update-V2.json");
        var legacy = ReadManifest("update.json");

        Assert.Equal(current.GetProperty("version").GetString(), legacy.GetProperty("version").GetString());
        Assert.Equal(current.GetProperty("dllLink").GetString(), legacy.GetProperty("dllLink").GetString());
    }

    [Fact]
    public void Feed_never_advertises_a_version_newer_than_the_source()
    {
        var manifest = ReadManifest("update-V2.json");
        var sourceVersion = SemVersion.Parse(ReadSourceVersionNumber());

        var isNewer = UpdateVersionCheck.IsNewerThanInstalled(
            manifest.GetProperty("valid").GetBoolean(),
            manifest.GetProperty("version").GetString()!,
            sourceVersion);

        Assert.False(isNewer, "api/update-V2.json advertises a version above VERSION_NUMBER; bump the source before publishing the feed.");
    }
}
