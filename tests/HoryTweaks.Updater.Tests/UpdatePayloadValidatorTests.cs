using BetterAmongUs.Modules.Updater;
using Semver;
using Xunit;

namespace BetterAmongUs.Tests;

public class UpdatePayloadValidatorTests
{
    private static readonly byte[] ThisAssembly = File.ReadAllBytes(typeof(UpdatePayloadValidatorTests).Assembly.Location);
    private static readonly string ThisAssemblyName = typeof(UpdatePayloadValidatorTests).Assembly.GetName().Name!;
    private static readonly Version ThisAssemblyVersion = typeof(UpdatePayloadValidatorTests).Assembly.GetName().Version!;
    private static readonly SemVersion ThisCoreVersion = new(ThisAssemblyVersion.Major, ThisAssemblyVersion.Minor, ThisAssemblyVersion.Build);
    private static readonly SemVersion Installed = new(0, 1, 0);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("http://github.com/owner/repo/releases/download/v1/HoryTweaks.dll")]
    [InlineData("ftp://github.com/HoryTweaks.dll")]
    [InlineData("/releases/download/HoryTweaks.dll")]
    public void TryGetDownloadUri_RejectsMissingOrNonHttpsLinks(string? link)
    {
        Assert.False(UpdatePayloadValidator.TryGetDownloadUri(link, out var uri));
        Assert.Null(uri);
    }

    [Fact]
    public void TryGetDownloadUri_AcceptsHttpsLink()
    {
        const string link = " https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.1/HoryTweaks.dll ";

        Assert.True(UpdatePayloadValidator.TryGetDownloadUri(link, out var uri));
        Assert.Equal("https://github.com/horizzon3507/HoryTweaks/releases/download/v0.1.1/HoryTweaks.dll", uri!.ToString());
    }

    [Fact]
    public void Validate_RejectsEmptyPayload()
    {
        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePayloadValidator.Validate(null, "HoryTweaks", Installed, ThisCoreVersion).Status);
        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePayloadValidator.Validate([], "HoryTweaks", Installed, ThisCoreVersion).Status);
    }

    [Fact]
    public void Validate_RejectsNonAssemblyBytes()
    {
        var html = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>Not Found</body></html>");
        var truncatedPe = ThisAssembly.Take(64).ToArray();

        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePayloadValidator.Validate(html, "HoryTweaks", Installed, ThisCoreVersion).Status);
        Assert.Equal(UpdateStatus.InvalidPayload, UpdatePayloadValidator.Validate(truncatedPe, "HoryTweaks", Installed, ThisCoreVersion).Status);
    }

    [Fact]
    public void Validate_RejectsAssemblyWithDifferentName()
    {
        var outcome = UpdatePayloadValidator.Validate(ThisAssembly, "HoryTweaks", Installed, ThisCoreVersion);

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.Contains(ThisAssemblyName, outcome.Detail);
    }

    [Fact]
    public void Validate_RejectsAssemblyThatDoesNotMatchAdvertisedVersion()
    {
        var advertised = new SemVersion(ThisAssemblyVersion.Major, ThisAssemblyVersion.Minor, ThisAssemblyVersion.Build + 1);

        var outcome = UpdatePayloadValidator.Validate(ThisAssembly, ThisAssemblyName, Installed, advertised);

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.Contains("does not match", outcome.Detail);
    }

    [Fact]
    public void Validate_RejectsAssemblyOlderThanInstalled()
    {
        var outcome = UpdatePayloadValidator.Validate(ThisAssembly, ThisAssemblyName, new SemVersion(99, 0, 0), ThisCoreVersion);

        Assert.Equal(UpdateStatus.InvalidPayload, outcome.Status);
        Assert.Contains("older", outcome.Detail);
    }

    [Fact]
    public void Validate_AcceptsAdvertisedAssemblyWithExpectedName()
    {
        var outcome = UpdatePayloadValidator.Validate(ThisAssembly, ThisAssemblyName.ToUpperInvariant(), Installed, ThisCoreVersion);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(UpdateStatus.Succeeded, outcome.Status);
    }

    [Fact]
    public void Validate_IgnoresPrereleaseTagsWhenComparingVersions()
    {
        var installedBeta = new SemVersion(ThisAssemblyVersion.Major, ThisAssemblyVersion.Minor, ThisAssemblyVersion.Build, ["beta", "1"]);
        var advertisedBeta = new SemVersion(ThisAssemblyVersion.Major, ThisAssemblyVersion.Minor, ThisAssemblyVersion.Build, ["beta", "2"]);

        Assert.True(UpdatePayloadValidator.Validate(ThisAssembly, ThisAssemblyName, installedBeta, advertisedBeta).IsSuccess);
        Assert.True(UpdatePayloadValidator.Validate(ThisAssembly, ThisAssemblyName, installedBeta, ThisCoreVersion).IsSuccess);
    }
}
