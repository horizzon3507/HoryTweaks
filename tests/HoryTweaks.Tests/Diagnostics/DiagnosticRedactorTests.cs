using HoryTweaks.Core.Diagnostics;
using Xunit;

namespace HoryTweaks.Tests.Diagnostics;

public class DiagnosticRedactorTests
{
    private const char Prefix = '\u2063';
    private const char Postfix = '\u2064';

    [Fact]
    public void Redact_RemovesFriendCodes()
    {
        var result = DiagnosticRedactor.Redact("Banned tastycrewmate#1234 and Other#0000 today");

        Assert.DoesNotContain("tastycrewmate#1234", result);
        Assert.DoesNotContain("Other#0000", result);
        Assert.Contains(DiagnosticRedactor.FriendCodePlaceholder, result);
    }

    [Fact]
    public void Redact_RemovesLobbyCodes()
    {
        var result = DiagnosticRedactor.Redact("[Info   :HoryTweaks] [OnGameJoinedPatch] Successfully joined QWERTZ\nCode: ABCD");

        Assert.DoesNotContain("QWERTZ", result);
        Assert.DoesNotContain("ABCD", result);
        Assert.Contains(DiagnosticRedactor.LobbyCodePlaceholder, result);
    }

    [Fact]
    public void Redact_KeepsOrdinaryLogText()
    {
        const string line = "[Info   :HoryTweaks] HoryTweaks 0.1.1 - [2026.9.29 --> 2026.9.29] Steam";

        Assert.Equal(line, DiagnosticRedactor.Redact(line));
    }

    [Fact]
    public void Redact_RemovesAddressesUserNamesAndSecrets()
    {
        var result = DiagnosticRedactor.Redact(
            "Connecting to 192.168.1.20:22023 from C:\\Users\\Alice\\AppData token=abc.def password: hunter2",
            ["Alice"]);

        Assert.DoesNotContain("192.168.1.20", result);
        Assert.DoesNotContain("Alice", result);
        Assert.DoesNotContain("abc.def", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.Contains(DiagnosticRedactor.AddressPlaceholder, result);
        Assert.Contains(DiagnosticRedactor.SecretPlaceholder, result);
    }

    [Fact]
    public void OmitPrivateEntries_ReplacesEncryptedPayloadWithoutDecrypting()
    {
        var log = $"[Info] plain line\n[Info] [ChatLog] {Prefix}ZW5jcnlwdGVk{Postfix}\r\n[Info] another {Prefix}unterminated";

        var result = DiagnosticRedactor.OmitPrivateEntries(log, Prefix, Postfix);

        Assert.DoesNotContain("ZW5jcnlwdGVk", result);
        Assert.DoesNotContain("unterminated", result);
        Assert.DoesNotContain(Prefix, result);
        Assert.Contains("[Info] plain line", result);
        Assert.Contains($"[Info] [ChatLog] {DiagnosticRedactor.PrivateEntryPlaceholder}\r", result);
        Assert.Contains($"[Info] another {DiagnosticRedactor.PrivateEntryPlaceholder}", result);
    }

    [Fact]
    public void TakeRecentLines_ReturnsTailWithoutTrailingBlankLines()
    {
        var text = string.Join("\r\n", Enumerable.Range(1, 10).Select(i => $"line {i}")) + "\r\n\r\n";

        var recent = DiagnosticRedactor.TakeRecentLines(text, 3);

        Assert.Equal(["line 8", "line 9", "line 10"], recent);
        Assert.Empty(DiagnosticRedactor.TakeRecentLines(text, 0));
        Assert.Equal(10, DiagnosticRedactor.TakeRecentLines(text, 500).Count);
    }
}
