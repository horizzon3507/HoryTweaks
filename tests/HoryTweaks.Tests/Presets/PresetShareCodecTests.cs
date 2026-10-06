using System.IO.Compression;
using System.Text;
using BetterAmongUs.Core.Presets;
using Xunit;

namespace HoryTweaks.Tests.Presets;

public class PresetShareCodecTests
{
    [Fact]
    public void RoundTrip_PreservesSupportedPrimitiveValues()
    {
        var settings = new Dictionary<string, object?>
        {
            ["KickCooldown"] = 15,
            ["RpcRateLimit"] = 2.5f,
            ["BanPlayerList"] = true,
            ["PresetName"] = "Streaming"
        };

        string code = PresetShareCodec.Encode(settings);

        Assert.StartsWith(PresetShareCodec.Prefix, code);
        Assert.True(PresetShareCodec.TryDecode(code, out var decoded));
        Assert.Equal(15, decoded["KickCooldown"]);
        Assert.Equal(2.5f, decoded["RpcRateLimit"]);
        Assert.Equal(true, decoded["BanPlayerList"]);
        Assert.Equal("Streaming", decoded["PresetName"]);
    }

    [Fact]
    public void Encode_SkipsInvalidKeysAndUnsupportedValues()
    {
        var settings = new Dictionary<string, object?>
        {
            ["Valid_Key"] = 1,
            ["has space"] = 2,
            ["Nested"] = new List<int> { 1 },
            ["Missing"] = null
        };

        Assert.True(PresetShareCodec.TryDecode(PresetShareCodec.Encode(settings), out var decoded));
        Assert.Single(decoded);
        Assert.Equal(1, decoded["Valid_Key"]);
    }

    [Fact]
    public void TryDecode_AcceptsSurroundingWhitespace()
    {
        string code = PresetShareCodec.Encode([new("A", 1)]);

        Assert.True(PresetShareCodec.TryDecode($"  {code}\n", out var decoded));
        Assert.Equal(1, decoded["A"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("HTP1:")]
    [InlineData("HTP1:not-base64!")]
    [InlineData("HTP2:H4sIAAAAAAAA")]
    [InlineData("H4sIAAAAAAAAA6tWSkksSVSyUlAqAAA=")]
    public void TryDecode_RejectsMalformedInput(string? text)
    {
        Assert.False(PresetShareCodec.TryDecode(text, out var decoded));
        Assert.Empty(decoded);
    }

    [Fact]
    public void TryDecode_RejectsBase64ThatIsNotGzip()
    {
        string code = PresetShareCodec.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"A\":1}"));

        Assert.False(PresetShareCodec.TryDecode(code, out _));
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    [InlineData("{}")]
    [InlineData("{\"A\":null}")]
    [InlineData("{\"A\":[1]}")]
    [InlineData("{\"A\":{\"B\":1}}")]
    [InlineData("{\"bad key\":1}")]
    [InlineData("{\"A\":1e40}")]
    [InlineData("{\"A\":1")]
    public void TryDecode_RejectsUnsupportedJsonShapes(string json)
    {
        Assert.False(PresetShareCodec.TryDecode(Wrap(json), out _));
    }

    [Fact]
    public void TryDecode_ConvertsWholeNumbersToIntAndFractionsToFloat()
    {
        Assert.True(PresetShareCodec.TryDecode(Wrap("{\"A\":3,\"B\":0.5,\"C\":-2}"), out var decoded));
        Assert.IsType<int>(decoded["A"]);
        Assert.IsType<float>(decoded["B"]);
        Assert.Equal(-2, decoded["C"]);
    }

    [Fact]
    public void TryDecode_RejectsTooManySettings()
    {
        var builder = new StringBuilder("{");
        for (int i = 0; i <= PresetShareCodec.MaxSettings; i++)
        {
            if (i > 0)
                builder.Append(',');
            builder.Append($"\"K{i}\":1");
        }
        builder.Append('}');

        Assert.False(PresetShareCodec.TryDecode(Wrap(builder.ToString()), out _));
    }

    [Fact]
    public void TryDecode_RejectsOversizedDecompressedPayload()
    {
        string json = "{\"A\":\"" + new string('x', 200 * 1024) + "\"}";

        Assert.False(PresetShareCodec.TryDecode(Wrap(json), out _));
    }

    private static string Wrap(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(json));
        }

        return PresetShareCodec.Prefix + Convert.ToBase64String(output.ToArray());
    }
}
