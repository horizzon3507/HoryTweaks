using Il2CppInterop.Runtime.Attributes;
using System.Collections;
using UnityEngine.Networking;

namespace BetterAmongUs.Network;

/// <summary>
/// Uploads the diagnostic report file to 0x0.st. Request building and response
/// handling live in <see cref="ReportUploadRequest"/>; this is the thin
/// UnityWebRequest boundary around it.
/// </summary>
internal static class ReportUploader
{
    /// <summary>
    /// POSTs <paramref name="filePath"/> to 0x0.st as multipart form field <c>file</c>.
    /// <paramref name="onComplete"/> is invoked exactly once: with the returned URL and
    /// a null error on success, or a null URL and a short failure detail. The local
    /// file is left untouched either way.
    /// </summary>
    /// <returns>IEnumerator for coroutine execution.</returns>
    [HideFromIl2Cpp]
    internal static IEnumerator CoUpload(string filePath, Action<string?, string?> onComplete)
    {
        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            onComplete(null, $"could not read the report file: {ex.Message}");
            yield break;
        }

        var boundary = ReportUploadRequest.NewBoundary();
        var body = ReportUploadRequest.BuildBody(fileBytes, Path.GetFileName(filePath), boundary, out var contentType);

        var www = new UnityWebRequest(ReportUploadRequest.Endpoint, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = ReportUploadRequest.UploadTimeoutSeconds,
        };
        www.SetRequestHeader("Content-Type", contentType);
        www.SetRequestHeader("User-Agent", ReportUploadRequest.BuildUserAgent(BAUPlugin.ModInfo.VERSION_STRING));

        yield return www.SendWebRequest();

        string? url = null;
        string? error = null;
        var responseText = www.downloadHandler?.text;
        if (www.result == UnityWebRequest.Result.Success &&
            ReportUploadRequest.TryGetUploadedUrl(responseText, www.responseCode, out var uploadedUrl))
        {
            url = uploadedUrl;
        }
        else
        {
            var kind = www.result switch
            {
                UnityWebRequest.Result.ConnectionError => ReportUploadRequest.FailureKind.Connection,
                UnityWebRequest.Result.ProtocolError => ReportUploadRequest.FailureKind.Http,
                _ => ReportUploadRequest.FailureKind.InvalidResponse,
            };
            error = ReportUploadRequest.DescribeFailure(kind, www.responseCode);
            BAUPlugin.Logger.Error($"Report upload to 0x0.st failed: {error} ({www.error})");
        }
        www.Dispose();

        onComplete(url, error);
    }
}
