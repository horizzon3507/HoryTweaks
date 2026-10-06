using BetterAmongUs.Core.Updates;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace HoryTweaks.Tests.Updates;

public class UpdatePackageValidatorPackageTests
{
    private static byte[] BuildZip(params string[] entryNames)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entryNames)
            {
                if (name.EndsWith('/'))
                {
                    archive.CreateEntry(name);
                    continue;
                }

                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write("payload");
            }
        }

        return stream.ToArray();
    }

    private static readonly string[] ValidEntries =
    [
        "winhttp.dll",
        "doorstop_config.ini",
        "BepInEx/core/BepInEx.Core.dll",
        "BepInEx/plugins/HoryTweaks.dll",
        "dotnet/runtime.dll",
    ];

    [Fact]
    public void Validate_accepts_a_release_package_shape()
    {
        var outcome = UpdatePackageValidator.Validate(BuildZip(ValidEntries), "HoryTweaks-Steam-Epic-MsStore-v0.1.3.zip");

        Assert.True(outcome.IsSuccess, outcome.Detail);
        Assert.Equal(UpdateStatus.Succeeded, outcome.Status);
    }

    [Fact]
    public void Validate_rejects_a_package_without_the_bootstrap_loader()
    {
        var zip = BuildZip("BepInEx/plugins/HoryTweaks.dll");

        var outcome = UpdatePackageValidator.Validate(zip, "pkg.zip");

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.Contains("winhttp.dll", outcome.Detail);
    }

    [Fact]
    public void Validate_rejects_a_package_without_the_mod_payload()
    {
        var zip = BuildZip("winhttp.dll", "BepInEx/plugins/SomeOtherMod.dll");

        var outcome = UpdatePackageValidator.Validate(zip, "pkg.zip");

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.Contains(UpdatePackageValidator.ModPayloadEntry, outcome.Detail);
    }

    [Fact]
    public void Validate_rejects_non_zip_and_empty_payloads()
    {
        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePackageValidator.Validate(null, "pkg.zip").Status);
        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePackageValidator.Validate([], "pkg.zip").Status);
        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePackageValidator.Validate(Encoding.UTF8.GetBytes("<html>404</html>"), "pkg.zip").Status);
    }

    [Fact]
    public void Validate_rejects_a_package_with_an_unsafe_path()
    {
        var zip = BuildZip("winhttp.dll", "BepInEx/plugins/HoryTweaks.dll", "../evil.dll");

        var outcome = UpdatePackageValidator.Validate(zip, "pkg.zip");

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.Contains("evil", outcome.Detail);
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("foo/../../evil.dll")]
    [InlineData("/absolute/path.dll")]
    [InlineData("C:\\windows\\evil.dll")]
    [InlineData("\\\\server\\share\\evil.dll")]
    [InlineData("dir\\..\\..\\evil.dll")]
    public void IsSafeEntryName_rejects_escaping_names(string entryName)
    {
        Assert.False(UpdatePackageValidator.IsSafeEntryName(entryName));
    }

    [Theory]
    [InlineData("winhttp.dll")]
    [InlineData("BepInEx/plugins/HoryTweaks.dll")]
    [InlineData("BepInEx\\plugins\\HoryTweaks.dll")]
    [InlineData("dir/sub dir/file name.txt")]
    public void IsSafeEntryName_accepts_normal_names(string entryName)
    {
        Assert.True(UpdatePackageValidator.IsSafeEntryName(entryName));
    }
}
