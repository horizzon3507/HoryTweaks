using BepInEx;

using Il2CppInterop.Runtime.Attributes;
using Semver;
using System.Collections;
using BetterAmongUs.Core.Updates;
using BetterAmongUs.Features.Hud;
using BetterAmongUs.Infrastructure.Persistence;

namespace BetterAmongUs.Remote.Updates;

/// <summary>
/// Downloads, validates and installs a mod update, reporting an explicit <see cref="UpdateOutcome"/>.
/// Prefers the store-specific full release package (verified against the release's
/// SHA256SUMS.txt and applied by a helper after the game exits) and falls back to the
/// assembly swap whenever the package path cannot be taken.
/// </summary>
internal static class BAUUpdateDownloader
{
    /// <summary>
    /// Runs the full update pipeline. <paramref name="onComplete"/> is invoked exactly once on every
    /// non-throwing path; the installed payload is only replaced after it has been validated,
    /// and it is left untouched (or rolled back) when any step fails.
    /// </summary>
    /// <param name="info">The update feed entry to install.</param>
    /// <param name="onComplete">Receives the final outcome.</param>
    /// <returns>IEnumerator for coroutine execution.</returns>
    [HideFromIl2Cpp]
    internal static IEnumerator CoDownloadAndInstall(BAUUpdateData info, Action<UpdateOutcome> onComplete)
    {
        var packageLink = UpdatePackageCatalog.SelectLink(info.Packages, DetectStoreKind());
        if (packageLink != null &&
            UpdatePayloadValidator.TryGetDownloadUri(packageLink, out var packageUri) && packageUri != null &&
            UpdatePayloadValidator.TryGetDownloadUri(info.Sha256Link, out var manifestUri) && manifestUri != null)
        {
            UpdateOutcome? packageOutcome = null;
            var packageRoutine = CoDownloadPackage(packageUri, manifestUri, result => packageOutcome = result);
            while (true)
            {
                bool moved;
                try
                {
                    moved = packageRoutine.MoveNext();
                }
                catch (Exception ex)
                {
                    BAUPlugin.Logger.Warning($"Store package update threw ({ex.Message}); falling back to the assembly swap.");
                    packageOutcome = null;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                yield return packageRoutine.Current;
            }

            if (packageOutcome != null && packageOutcome.Obtained)
            {
                onComplete(packageOutcome);
                yield break;
            }

            var packageStatus = packageOutcome == null ? "crashed" : packageOutcome.Status.ToString();
            var packageDetail = packageOutcome == null ? "no outcome reported" : packageOutcome.Detail;
            BAUPlugin.Logger.Warning($"Store package update could not be applied ({packageStatus}): {packageDetail}. Falling back to the assembly swap.");
        }

        if (!UpdatePayloadValidator.TryGetDownloadUri(info.DllLink, out var downloadUri) || downloadUri == null)
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.MissingDownloadLink, $"The update feed link '{info.DllLink}' is missing or not an https URL."));
            yield break;
        }

        var installedPath = BAUPlugin.ModInfo.Assembly.Location;
        if (string.IsNullOrEmpty(installedPath) || !File.Exists(installedPath))
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.InstallFailed, $"The installed assembly path '{installedPath}' could not be resolved."));
            yield break;
        }

        if (!GithubAPI.IsInternetAvailable())
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.NoInternet, "The connectivity probe failed."));
            yield break;
        }

        byte[]? payload = null;
        string? downloadError = null;
        yield return GitHubFile.CoDownloadBytes(downloadUri.ToString(), (bytes, error) =>
        {
            payload = bytes;
            downloadError = error;
        }, true);

        if (payload == null)
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.DownloadFailed, downloadError ?? "The download ended without a result."));
            yield break;
        }

        if (!SemVersion.TryParse(info.Version, SemVersionStyles.Any, out var advertisedVersion))
        {
            CustomLoadingBarManager.ToggleLoadingBar(false);
            onComplete(UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The update feed version '{info.Version}' is not a valid semantic version."));
            yield break;
        }

        var validation = UpdatePayloadValidator.Validate(payload, BAUPlugin.ModInfo.Assembly.GetName().Name ?? BAUPlugin.ModInfo.PLUGIN_NAME, BAUPlugin.ModInfo.SemVersion, advertisedVersion);
        if (!validation.IsSuccess)
        {
            CustomLoadingBarManager.ToggleLoadingBar(false);
            onComplete(validation);
            yield break;
        }

        BAUPlugin.Logger.Log($"Update payload accepted ({validation.Detail}), feed version {info.Version}.");
        CustomLoadingBarManager.SetLoadingPercent(100f, Generated.TranslationStrings.Update_Progress_Installing.LocalizedString);

        var stagedPath = UpdateAssemblyInstaller.StagingPath(installedPath);
        var outcome = UpdateAssemblyInstaller.Stage(stagedPath, payload);
        if (outcome.IsSuccess)
        {
            outcome = UpdateAssemblyInstaller.Install(installedPath, stagedPath);
        }

        CustomLoadingBarManager.ToggleLoadingBar(false);
        onComplete(outcome);
    }

    /// <summary>
    /// Store-variant package path: downloads the release's checksum manifest, downloads the
    /// package, verifies its SHA-256 against the manifest, validates the zip payload and
    /// hands it to the staging flow. Every failure returns an outcome describing why the
    /// package path could not be taken so the caller can fall back to the assembly swap.
    /// </summary>
    [HideFromIl2Cpp]
    private static IEnumerator CoDownloadPackage(Uri packageUri, Uri manifestUri, Action<UpdateOutcome> onComplete)
    {
        if (!GithubAPI.IsInternetAvailable())
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.NoInternet, "The connectivity probe failed."));
            yield break;
        }

        string manifestText = string.Empty;
        yield return GitHubFile.CoFetchTextFile(manifestUri.ToString(), text => manifestText = text);
        if (string.IsNullOrEmpty(manifestText))
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.DownloadFailed, $"The checksum manifest could not be downloaded from '{manifestUri}'."));
            yield break;
        }

        var fileName = UpdatePackageCatalog.AssetFileName(packageUri);
        if (fileName == null)
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.MissingDownloadLink, $"The package link '{packageUri}' has no file name to match in the checksum manifest."));
            yield break;
        }

        if (!UpdateSha256Manifest.TryGetExpectedHash(manifestText, fileName, out var expectedHex))
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The checksum manifest has no entry for '{fileName}'."));
            yield break;
        }

        byte[]? package = null;
        string? downloadError = null;
        yield return GitHubFile.CoDownloadBytes(packageUri.ToString(), (bytes, error) =>
        {
            package = bytes;
            downloadError = error;
        }, true);

        if (package == null)
        {
            onComplete(UpdateOutcome.Failure(UpdateStatus.DownloadFailed, downloadError ?? "The package download ended without a result."));
            yield break;
        }

        if (!UpdateSha256Manifest.HashMatches(package, expectedHex))
        {
            CustomLoadingBarManager.ToggleLoadingBar(false);
            onComplete(UpdateOutcome.Failure(UpdateStatus.InvalidPayload, $"The package '{fileName}' does not match the release checksum (expected {expectedHex}, got {UpdateSha256Manifest.ComputeSha256(package)})."));
            yield break;
        }

        var validation = UpdatePackageValidator.Validate(package, fileName);
        if (!validation.IsSuccess)
        {
            CustomLoadingBarManager.ToggleLoadingBar(false);
            onComplete(validation);
            yield break;
        }

        BAUPlugin.Logger.Log($"Update package accepted ({validation.Detail}).");

        var updateRoot = Path.Combine(BetterDataManager.GetPathToAmongUsData(), UpdatePackageInstaller.UpdateFolderName);
        var save = UpdatePackageInstaller.SavePackage(package, updateRoot, fileName);
        var savedPackagePath = save.IsSuccess ? save.Detail : null;

        var executablePath = string.IsNullOrEmpty(Paths.ExecutablePath) ? Environment.ProcessPath : Paths.ExecutablePath;
        var imageName = string.IsNullOrEmpty(executablePath) ? "Among Us.exe" : Path.GetFileName(executablePath);

        var schedule = UpdatePackageInstaller.StageAndSchedule(package, updateRoot, imageName, Paths.GameRootPath, string.IsNullOrEmpty(executablePath) ? null : executablePath);
        if (schedule.Status != UpdateStatus.Staged && savedPackagePath != null)
        {
            schedule = new UpdateOutcome(UpdateStatus.SavedToDisk, savedPackagePath);
        }

        CustomLoadingBarManager.ToggleLoadingBar(false);
        onComplete(schedule);
    }

    /// <summary>
    /// Maps the game's reported store platform to the release package variant that covers it.
    /// Platforms without a package (consoles, mobile, macOS, unknown) return
    /// <see cref="UpdateStoreKind.Unknown"/> so the caller falls back to the assembly swap.
    /// </summary>
    private static UpdateStoreKind DetectStoreKind()
    {
        try
        {
            var platform = BAUPlugin.PlatformData.Platform;
            if (platform == Platforms.StandaloneItch)
            {
                return UpdateStoreKind.Itchio;
            }

            if (platform is Platforms.StandaloneSteamPC or Platforms.StandaloneEpicPC or Platforms.StandaloneWin10)
            {
                return UpdateStoreKind.SteamEpicMsStore;
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Warning($"Store detection for the updater failed: {ex.Message}");
        }

        return UpdateStoreKind.Unknown;
    }
}
