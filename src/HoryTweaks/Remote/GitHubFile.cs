

using HoryTweaks.Generated;
using Il2CppInterop.Runtime.Attributes;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using HoryTweaks.Features.Hud;

namespace HoryTweaks.Remote;

/// <summary>
/// Provides methods for downloading files from GitHub repositories.
/// </summary>
internal static class GitHubFile
{
    /// <summary>
    /// Seconds without any new bytes before a download is aborted.
    /// </summary>
    internal const float DownloadStallTimeoutSeconds = 30f;

    /// <summary>
    /// Upper bound for a single download, whatever its progress.
    /// </summary>
    internal const float DownloadMaxDurationSeconds = 600f;

    /// <summary>
    /// Downloads an individual file from the remote repository into memory.
    /// </summary>
    /// <param name="url">The URL of the file to download.</param>
    /// <param name="onComplete">
    /// Always invoked exactly once: with the downloaded bytes and a null error on success,
    /// or with null bytes and a description of the failure.
    /// </param>
    /// <param name="showProgress">Whether to show a progress bar during download.</param>
    /// <returns>IEnumerator for coroutine execution.</returns>
    /// <remarks>
    /// The download is aborted when no bytes arrive for <see cref="DownloadStallTimeoutSeconds"/>
    /// or when it runs longer than <see cref="DownloadMaxDurationSeconds"/>, so callers never wait forever.
    /// The loading bar is hidden again on every path when <paramref name="showProgress"/> is set.
    /// </remarks>
    [HideFromIl2Cpp]
    internal static IEnumerator CoDownloadBytes(string url, Action<byte[]?, string?> onComplete, bool showProgress = false)
    {
        var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
        {
            downloadHandler = new DownloadHandlerBuffer()
        };

        if (showProgress)
        {
            CustomLoadingBarManager.ToggleLoadingBar(true);
            CustomLoadingBarManager.SetLoadingPercent(0f, TranslationStrings.Update_Progress_Starting.Format("..."));
        }

        var operation = www.SendWebRequest();

        float startedAt = Time.realtimeSinceStartup;
        float lastProgressAt = startedAt;
        ulong lastDownloadedBytes = 0;
        string? timeoutError = null;

        while (!operation.isDone)
        {
            float now = Time.realtimeSinceStartup;
            if (www.downloadedBytes != lastDownloadedBytes)
            {
                lastDownloadedBytes = www.downloadedBytes;
                lastProgressAt = now;
            }

            if (now - lastProgressAt > DownloadStallTimeoutSeconds)
            {
                timeoutError = $"no data received for {DownloadStallTimeoutSeconds:0} seconds";
            }
            else if (now - startedAt > DownloadMaxDurationSeconds)
            {
                timeoutError = $"download exceeded {DownloadMaxDurationSeconds:0} seconds";
            }

            if (timeoutError != null)
            {
                www.Abort();
                break;
            }

            if (showProgress)
            {
                int dotCount = (int)(Time.time * 2f) % 4;
                string dots = new('.', dotCount);
                float progress = www.downloadProgress * 100f;
                if (progress < 1f)
                {
                    CustomLoadingBarManager.SetLoadingPercent(0f, TranslationStrings.Update_Progress_Starting.Format(dots));
                }
                else
                {
                    CustomLoadingBarManager.SetLoadingPercent(progress, TranslationStrings.Update_Progress_Downloading.Format(dots));
                }
            }
            yield return null;
        }

        byte[]? bytes = null;
        string? error = timeoutError;
        if (error == null)
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    bytes = www.downloadHandler.GetNativeData().ToArray();
                }
                catch (Exception ex)
                {
                    error = $"could not read the response body: {ex.Message}";
                }
            }
            else
            {
                error = $"{www.error} (Response Code: {(int)www.responseCode})";
            }
        }
        www.Dispose();

        if (error != null)
        {
            BAUPlugin.Logger.Error($"Error downloading file from URL '{url}': {error}");
            if (showProgress)
            {
                CustomLoadingBarManager.SetLoadingPercent(100f, TranslationStrings.Update_Progress_Failed.LocalizedString);
                yield return new WaitForSeconds(2f);
                CustomLoadingBarManager.ToggleLoadingBar(false);
            }
            onComplete(null, error);
            yield break;
        }

        if (showProgress)
        {
            CustomLoadingBarManager.SetLoadingPercent(100f, TranslationStrings.Update_Progress_Verifying.LocalizedString);
        }

        BAUPlugin.Logger.Log($"Downloaded {bytes!.Length} bytes from '{url}'.");
        onComplete(bytes, null);
    }

    /// <summary>
    /// Downloads a text file from the remote repository.
    /// </summary>
    /// <param name="url">The URL of the text file to download.</param>
    /// <param name="Callback">Callback to execute with the downloaded text.</param>
    /// <returns>IEnumerator for coroutine execution.</returns>
    [HideFromIl2Cpp]
    internal static IEnumerator CoFetchTextFile(string url, Action<string> Callback)
    {
        var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
        {
            downloadHandler = new DownloadHandlerBuffer()
        };
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            BAUPlugin.Logger.Error($"Error downloading {url}: {www.error}");
            yield break;
        }

        var response = www.downloadHandler.text;
        www.Dispose();
        Callback.Invoke(response);
    }
}