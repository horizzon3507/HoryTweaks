using System.Text;

namespace BetterAmongUs.Network;

/// <summary>
/// Unity-independent pieces of the 0x0.st report upload: multipart body building,
/// response parsing and failure wording. Kept free of Unity and IL2CPP references
/// so the request contract can be unit tested directly.
/// </summary>
internal static class ReportUploadRequest
{
    /// <summary>Endpoint the report is POSTed to.</summary>
    internal const string Endpoint = "https://0x0.st";

    /// <summary>Multipart form field 0x0.st expects the file under.</summary>
    internal const string FileFieldName = "file";

    /// <summary>Seconds before an unfinished upload is aborted.</summary>
    internal const int UploadTimeoutSeconds = 60;

    /// <summary>
    /// Failure buckets for an upload, mirroring the <c>UnityWebRequest.Result</c>
    /// values the uploader can hit. Kept as its own enum so this class stays
    /// free of Unity references and can be unit tested directly.
    /// </summary>
    internal enum FailureKind
    {
        /// The host could not be reached at all (offline, DNS, timeout).
        Connection,

        /// The host answered with a non-success HTTP status.
        Http,

        /// The request completed but the response did not contain a usable URL.
        InvalidResponse,
    }

    /// <summary>Generates a unique multipart boundary marker.</summary>
    internal static string NewBoundary() => "----HoryTweaks" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Builds a multipart/form-data body carrying <paramref name="fileBytes"/> under the
    /// <c>file</c> field 0x0.st expects, and returns the matching Content-Type header value.
    /// </summary>
    internal static byte[] BuildBody(byte[] fileBytes, string fileName, string boundary, out string contentType)
    {
        contentType = $"multipart/form-data; boundary={boundary}";

        var preamble = Encoding.UTF8.GetBytes(
            $"--{boundary}\r\nContent-Disposition: form-data; name=\"{FileFieldName}\"; filename=\"{fileName}\"\r\nContent-Type: text/plain\r\n\r\n");
        var epilogue = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

        var body = new byte[preamble.Length + fileBytes.Length + epilogue.Length];
        Buffer.BlockCopy(preamble, 0, body, 0, preamble.Length);
        Buffer.BlockCopy(fileBytes, 0, body, preamble.Length, fileBytes.Length);
        Buffer.BlockCopy(epilogue, 0, body, preamble.Length + fileBytes.Length, epilogue.Length);
        return body;
    }

    /// <summary>User-Agent sent with the upload, identifying the mod and its version.</summary>
    internal static string BuildUserAgent(string modVersion) =>
        $"HoryTweaks/{modVersion} (+https://github.com/horizzon3507/HoryTweaks)";

    /// <summary>
    /// Extracts the uploaded file URL from a 0x0.st response. A success answer is the
    /// bare URL as plain text; anything else (error text, markup, other hosts) is rejected.
    /// </summary>
    internal static bool TryGetUploadedUrl(string? responseText, long statusCode, out string url)
    {
        url = string.Empty;
        if (statusCode < 200 || statusCode >= 300)
            return false;

        var candidate = responseText?.Trim();
        if (string.IsNullOrEmpty(candidate) || candidate.Any(char.IsWhiteSpace))
            return false;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (!uri.Host.Equals("0x0.st", StringComparison.OrdinalIgnoreCase))
            return false;

        url = candidate;
        return true;
    }

    /// <summary>Short user-facing detail for a failed upload, interpolated into the chat message.</summary>
    internal static string DescribeFailure(FailureKind kind, long statusCode) => kind switch
    {
        FailureKind.Connection => "could not reach 0x0.st",
        FailureKind.Http => $"0x0.st rejected the upload (HTTP {statusCode})",
        _ => "unexpected response from 0x0.st",
    };
}
