using Semver;

namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Pure decision logic for update manifests.
/// </summary>
internal static class UpdateVersionCheck
{
    /// <summary>
    /// Determines whether a manifest advertises a version with higher precedence than the installed one.
    /// </summary>
    /// <param name="valid">The manifest <c>valid</c> flag; invalid manifests never trigger an update.</param>
    /// <param name="version">The manifest version string.</param>
    /// <param name="installedVersion">The version of the installed plugin.</param>
    /// <exception cref="FormatException"><paramref name="version"/> is not a strict semantic version.</exception>
    internal static bool IsNewerThanInstalled(bool valid, string version, SemVersion installedVersion)
    {
        if (!valid)
        {
            return false;
        }

        var updateVersion = SemVersion.Parse(version);
        return updateVersion.ComparePrecedenceTo(installedVersion) > 0;
    }
}
