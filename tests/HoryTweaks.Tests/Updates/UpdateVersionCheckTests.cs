using BetterAmongUs.Core.Updates;
using Semver;
using Xunit;

namespace HoryTweaks.Tests.Updates;

public class UpdateVersionCheckTests
{
    private static readonly SemVersion Installed = SemVersion.Parse("0.1.1");

    [Theory]
    [InlineData("0.1.2", true)]
    [InlineData("0.2.0", true)]
    [InlineData("1.0.0", true)]
    [InlineData("0.1.2-beta.1", true)]
    [InlineData("0.1.1", false)]
    [InlineData("0.1.0", false)]
    [InlineData("0.0.9", false)]
    [InlineData("0.1.1-beta.1", false)]
    [InlineData("0.1.1+build.7", false)]
    public void Valid_manifest_is_new_only_when_precedence_is_higher(string manifestVersion, bool expected)
    {
        Assert.Equal(expected, UpdateVersionCheck.IsNewerThanInstalled(true, manifestVersion, Installed));
    }

    [Theory]
    [InlineData("0.1.2")]
    [InlineData("9.9.9")]
    [InlineData("not-a-version")]
    public void Invalid_manifest_never_triggers_an_update(string manifestVersion)
    {
        Assert.False(UpdateVersionCheck.IsNewerThanInstalled(false, manifestVersion, Installed));
    }

    [Theory]
    [InlineData("0.1.1-beta-1", "0.1.1", true)]
    [InlineData("0.1.1-beta-1", "0.1.1-beta-2", true)]
    [InlineData("0.1.1-beta-2", "0.1.1-beta-1", false)]
    [InlineData("0.1.1-beta-1", "0.1.0", false)]
    public void Beta_installations_update_to_the_matching_stable_release(string installed, string manifestVersion, bool expected)
    {
        var installedVersion = SemVersion.Parse(installed);

        Assert.Equal(expected, UpdateVersionCheck.IsNewerThanInstalled(true, manifestVersion, installedVersion));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v0.1.2")]
    [InlineData("0.1")]
    [InlineData("0.1.2.3")]
    [InlineData("latest")]
    public void Malformed_versions_are_rejected_instead_of_treated_as_updates(string manifestVersion)
    {
        Assert.Throws<FormatException>(() => UpdateVersionCheck.IsNewerThanInstalled(true, manifestVersion, Installed));
    }
}
