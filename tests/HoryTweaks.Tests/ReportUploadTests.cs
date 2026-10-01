using BetterAmongUs.Network;
using System.Text;
using Xunit;

namespace HoryTweaks.Tests;

public class ReportUploadTests
{
    private const string Boundary = "----HoryTweaksTestBoundary0123456789abcdef";

    // Exercises the request contract against canned responses; no live 0x0.st calls.
    [Fact]
    public void BuildBody_WrapsFileInMultipartForm()
    {
        var fileBytes = Encoding.UTF8.GetBytes("report contents");

        var body = ReportUploadRequest.BuildBody(fileBytes, "report.txt", Boundary, out var contentType);
        var bodyText = Encoding.UTF8.GetString(body);

        Assert.Equal($"multipart/form-data; boundary={Boundary}", contentType);
        Assert.StartsWith($"--{Boundary}\r\n", bodyText);
        Assert.Contains("Content-Disposition: form-data; name=\"file\"; filename=\"report.txt\"\r\n", bodyText);
        Assert.Contains("Content-Type: text/plain\r\n\r\n", bodyText);
        Assert.EndsWith($"\r\n--{Boundary}--\r\n", bodyText);
    }

    [Fact]
    public void BuildBody_EmbedsFileBytesVerbatim()
    {
        var fileBytes = new byte[] { 0x00, 0xFF, 0x0A, 0x0D, 0x80, 0x7F };

        var body = ReportUploadRequest.BuildBody(fileBytes, "report.txt", Boundary, out _);

        var headerEnd = IndexOf(body, Encoding.ASCII.GetBytes("\r\n\r\n")) + 4;
        var footerStart = body.Length - Encoding.ASCII.GetByteCount($"\r\n--{Boundary}--\r\n");
        Assert.Equal(fileBytes, body[headerEnd..footerStart]);
    }

    [Fact]
    public void TryGetUploadedUrl_AcceptsPlainTextUrl()
    {
        Assert.True(ReportUploadRequest.TryGetUploadedUrl("https://0x0.st/xY7z.txt\n", 200, out var url));
        Assert.Equal("https://0x0.st/xY7z.txt", url);
    }

    [Fact]
    public void TryGetUploadedUrl_AcceptsUrlWithTokenAndExtension()
    {
        Assert.True(ReportUploadRequest.TryGetUploadedUrl("https://0x0.st/~abc-def_diagnostics.txt", 201, out var url));
        Assert.Equal("https://0x0.st/~abc-def_diagnostics.txt", url);
    }

    [Theory]
    [InlineData("https://0x0.st/xY7z.txt", 500)]
    [InlineData("https://0x0.st/xY7z.txt", 0)]
    [InlineData("error: file too large", 413)]
    [InlineData("<html>blocked</html>", 200)]
    [InlineData("https://evil.example/0x0.st.txt", 200)]
    [InlineData("http://0x0.st/xY7z.txt", 200)]
    [InlineData("https://0x0.st/a.txt https://0x0.st/b.txt", 200)]
    [InlineData("", 200)]
    [InlineData(null, 200)]
    public void TryGetUploadedUrl_RejectsAnythingButASingleHttpsUrlOn0x0st(string? responseText, long statusCode)
    {
        Assert.False(ReportUploadRequest.TryGetUploadedUrl(responseText, statusCode, out var url));
        Assert.Equal(string.Empty, url);
    }

    [Fact]
    public void DescribeFailure_MapsConnectionErrors()
    {
        Assert.Equal("could not reach 0x0.st",
            ReportUploadRequest.DescribeFailure(ReportUploadRequest.FailureKind.Connection, 0));
    }

    [Fact]
    public void DescribeFailure_IncludesHttpStatus()
    {
        Assert.Equal("0x0.st rejected the upload (HTTP 413)",
            ReportUploadRequest.DescribeFailure(ReportUploadRequest.FailureKind.Http, 413));
    }

    [Fact]
    public void DescribeFailure_CoversUnexpectedResponses()
    {
        Assert.Equal("unexpected response from 0x0.st",
            ReportUploadRequest.DescribeFailure(ReportUploadRequest.FailureKind.InvalidResponse, 200));
    }

    [Fact]
    public void BuildUserAgent_IdentifiesModAndVersion()
    {
        var agent = ReportUploadRequest.BuildUserAgent("0.1.2-beta");

        Assert.StartsWith("HoryTweaks/0.1.2-beta", agent);
        Assert.Contains("github.com/horizzon3507/HoryTweaks", agent);
    }

    [Fact]
    public void NewBoundary_IsUniquePerCall()
    {
        var first = ReportUploadRequest.NewBoundary();
        var second = ReportUploadRequest.NewBoundary();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("\r", first);
        Assert.DoesNotContain("\n", first);
        Assert.DoesNotContain("\"", first);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var found = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
}
