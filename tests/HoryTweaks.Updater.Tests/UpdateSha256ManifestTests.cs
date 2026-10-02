using BetterAmongUs.Modules.Updater;
using System.Text;
using Xunit;

namespace BetterAmongUs.Tests;

public class UpdateSha256ManifestTests
{
    // sha256("abc") — a fixed well-known digest.
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string OtherSha256 = "88d4266fd4e6338d13b845fcf289579d209c897823b9217da3e161936f031589";

    private static readonly string Manifest = string.Join('\n',
        $"{AbcSha256}  HoryTweaks-Steam-Epic-MsStore-v0.1.3.zip",
        $"{OtherSha256} *HoryTweaks-Itchio-v0.1.3.zip",
        "",
        "not a hash line",
        $"{OtherSha256}  HoryTweaks.dll");

    [Fact]
    public void TryGetExpectedHash_finds_entries_with_and_without_binary_marker()
    {
        Assert.True(UpdateSha256Manifest.TryGetExpectedHash(Manifest, "HoryTweaks-Steam-Epic-MsStore-v0.1.3.zip", out var hash));
        Assert.Equal(AbcSha256, hash);

        Assert.True(UpdateSha256Manifest.TryGetExpectedHash(Manifest, "HoryTweaks-Itchio-v0.1.3.zip", out var binHash));
        Assert.Equal(OtherSha256, binHash);
    }

    [Fact]
    public void TryGetExpectedHash_matches_names_case_insensitively_and_handles_crlf()
    {
        var crlf = Manifest.Replace("\n", "\r\n");

        Assert.True(UpdateSha256Manifest.TryGetExpectedHash(crlf, "horytweaks-steam-epic-msstore-v0.1.3.ZIP", out var hash));
        Assert.Equal(AbcSha256, hash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SHA256SUMS.txt")]
    public void TryGetExpectedHash_returns_false_for_missing_entries(string? manifest)
    {
        Assert.False(UpdateSha256Manifest.TryGetExpectedHash(manifest, "Missing.zip", out var hash));
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void ComputeSha256_returns_lowercase_hex_digest()
    {
        Assert.Equal(AbcSha256, UpdateSha256Manifest.ComputeSha256(Encoding.UTF8.GetBytes("abc")));
    }

    [Fact]
    public void HashMatches_accepts_matching_digest_regardless_of_case()
    {
        var payload = Encoding.UTF8.GetBytes("abc");

        Assert.True(UpdateSha256Manifest.HashMatches(payload, AbcSha256));
        Assert.True(UpdateSha256Manifest.HashMatches(payload, AbcSha256.ToUpperInvariant()));
    }

    [Fact]
    public void HashMatches_rejects_mismatched_or_malformed_digests()
    {
        var payload = Encoding.UTF8.GetBytes("abc");

        Assert.False(UpdateSha256Manifest.HashMatches(payload, OtherSha256));
        Assert.False(UpdateSha256Manifest.HashMatches(payload, "not-a-hex-string"));
        Assert.False(UpdateSha256Manifest.HashMatches(payload, string.Empty));
    }
}
