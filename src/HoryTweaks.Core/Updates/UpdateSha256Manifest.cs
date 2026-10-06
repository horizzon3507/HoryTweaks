using System.Security.Cryptography;

namespace BetterAmongUs.Core.Updates;

/// <summary>
/// Parses a GNU <c>sha256sum</c> manifest and verifies payloads against it.
/// </summary>
internal static class UpdateSha256Manifest
{
    /// <summary>
    /// Finds the expected SHA-256 for <paramref name="fileName"/> in manifest text shaped like
    /// <c>&lt;64 hex chars&gt;  &lt;file name&gt;</c> per line (a <c>*</c> before the name marks
    /// binary mode and is accepted). File names are compared case-insensitively.
    /// </summary>
    internal static bool TryGetExpectedHash(string? manifestText, string fileName, out string expectedHex)
    {
        expectedHex = string.Empty;
        if (string.IsNullOrEmpty(manifestText) || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (var rawLine in manifestText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length < 66 || !IsHex(line.AsSpan(0, 64)))
            {
                continue;
            }

            var entryName = line[64..].TrimStart();
            if (entryName.StartsWith('*'))
            {
                entryName = entryName[1..];
            }

            if (string.Equals(entryName.Trim(), fileName, StringComparison.OrdinalIgnoreCase))
            {
                expectedHex = line[..64].ToLowerInvariant();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Computes the lowercase hex SHA-256 of <paramref name="payload"/>.
    /// </summary>
    internal static string ComputeSha256(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    /// <summary>
    /// Checks whether <paramref name="payload"/> hashes to the expected manifest digest.
    /// The comparison runs in constant time on the raw digest bytes.
    /// </summary>
    internal static bool HashMatches(byte[] payload, string expectedHex)
    {
        if (expectedHex.Length != 64 || !IsHex(expectedHex.AsSpan()))
        {
            return false;
        }

        var expected = Convert.FromHexString(expectedHex);
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), expected);
    }

    private static bool IsHex(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            var isHexDigit = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!isHexDigit)
            {
                return false;
            }
        }

        return true;
    }
}
