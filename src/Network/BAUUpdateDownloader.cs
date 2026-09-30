using BetterAmongUs.Managers;
using BetterAmongUs.Modules.Updater;
using BetterAmongUs.Network.Configs;
using Il2CppInterop.Runtime.Attributes;
using Semver;
using System.Collections;

namespace BetterAmongUs.Network;

/// <summary>
/// Downloads, validates and installs a mod update, reporting an explicit <see cref="UpdateOutcome"/>.
/// </summary>
internal static class BAUUpdateDownloader
{
    /// <summary>
    /// Runs the full update pipeline. <paramref name="onComplete"/> is invoked exactly once on every
    /// non-throwing path; the installed assembly is only replaced after the payload has been validated,
    /// and it is left untouched (or rolled back) when any step fails.
    /// </summary>
    /// <param name="info">The update feed entry to install.</param>
    /// <param name="onComplete">Receives the final outcome.</param>
    /// <returns>IEnumerator for coroutine execution.</returns>
    [HideFromIl2Cpp]
    internal static IEnumerator CoDownloadAndInstall(BAUUpdateData info, Action<UpdateOutcome> onComplete)
    {
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
}
