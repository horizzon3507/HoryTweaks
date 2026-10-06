using HoryTweaks.Core.Updates;
using System.Text.Json;
using Xunit;

namespace HoryTweaks.Tests.Updates;

public class UpdatePackageCatalogTests
{
    private const string FeedJson = """
        {
          "valid": true,
          "dllLink": "https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.3/HoryTweaks.dll",
          "version": "0.1.3",
          "packages": {
            "steamEpicMsStore": "https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.3/HoryTweaks-Steam-Epic-MsStore-v0.1.3.zip",
            "itchio": "https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.3/HoryTweaks-Itchio-v0.1.3.zip"
          },
          "sha256Link": "https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.3/SHA256SUMS.txt"
        }
        """;

    [Fact]
    public void Feed_packages_object_deserializes_with_expected_links()
    {
        var packages = JsonDocument.Parse(FeedJson).RootElement.GetProperty("packages").Deserialize<UpdatePackageLinks>();

        Assert.NotNull(packages);
        Assert.Equal("https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.3/HoryTweaks-Steam-Epic-MsStore-v0.1.3.zip", packages!.SteamEpicMsStore);
        Assert.Equal("https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.3/HoryTweaks-Itchio-v0.1.3.zip", packages.Itchio);
    }

    [Fact]
    public void SelectLink_maps_each_store_kind_to_its_package()
    {
        var packages = JsonDocument.Parse(FeedJson).RootElement.GetProperty("packages").Deserialize<UpdatePackageLinks>();

        Assert.Equal(packages!.SteamEpicMsStore, UpdatePackageCatalog.SelectLink(packages, UpdateStoreKind.SteamEpicMsStore));
        Assert.Equal(packages.Itchio, UpdatePackageCatalog.SelectLink(packages, UpdateStoreKind.Itchio));
        Assert.Null(UpdatePackageCatalog.SelectLink(packages, UpdateStoreKind.Unknown));
    }

    [Fact]
    public void SelectLink_returns_null_without_package_entries()
    {
        Assert.Null(UpdatePackageCatalog.SelectLink(null, UpdateStoreKind.SteamEpicMsStore));
        Assert.Null(UpdatePackageCatalog.SelectLink(new UpdatePackageLinks(), UpdateStoreKind.SteamEpicMsStore));
        Assert.Null(UpdatePackageCatalog.SelectLink(new UpdatePackageLinks { Itchio = "https://example.com/a.zip" }, UpdateStoreKind.SteamEpicMsStore));
    }

    [Theory]
    [InlineData("https://github.com/o/r/releases/download/v1/HoryTweaks-Itchio-v1.zip", "HoryTweaks-Itchio-v1.zip")]
    [InlineData("https://example.com/HoryTweaks%20pkg.zip?sig=abc", "HoryTweaks pkg.zip")]
    public void AssetFileName_returns_the_last_url_segment(string url, string expected)
    {
        Assert.Equal(expected, UpdatePackageCatalog.AssetFileName(new Uri(url)));
    }

    [Fact]
    public void AssetFileName_returns_null_for_a_root_url()
    {
        Assert.Null(UpdatePackageCatalog.AssetFileName(new Uri("https://example.com/")));
    }
}
