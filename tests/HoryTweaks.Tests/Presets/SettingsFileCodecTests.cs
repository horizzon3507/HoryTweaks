using BetterAmongUs.Core.Presets;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace HoryTweaks.Tests.Presets;

public class SettingsFileCodecTests
{
    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Dictionary<string, object?> LoadSettings(string fileJson)
    {
        using var document = JsonDocument.Parse(fileJson);
        return document.RootElement
            .GetProperty(SettingsFileCodec.SETTINGS_PROPERTY)
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => SettingsFileCodec.ConvertElement(property.Value));
    }

    [Theory]
    [InlineData("7", 7)]
    [InlineData("-3", -3)]
    [InlineData("1.0", 1f)]
    [InlineData("2.5", 2.5f)]
    [InlineData("3000000000", 3000000000f)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"text\"", "text")]
    public void ConvertElement_maps_json_primitives_to_setting_types(string json, object expected)
    {
        var actual = SettingsFileCodec.ConvertElement(Element(json));

        Assert.Equal(expected.GetType(), actual!.GetType());
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{\"nested\": 1}")]
    public void ConvertElement_rejects_unsupported_kinds(string json)
    {
        var element = Element(json);

        Assert.Throws<NotSupportedException>(() => SettingsFileCodec.ConvertElement(element));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("2.0", true)]
    [InlineData("2.1", false)]
    public void RequiresMigration_only_skips_the_current_version(string? storedVersion, bool expected)
    {
        Assert.Equal(expected, SettingsFileCodec.RequiresMigration(storedVersion));
    }

    [Theory]
    [InlineData("2.0", "Settings.json.v2.0.bak")]
    [InlineData(null, "Settings.json.vunknown.bak")]
    [InlineData("   ", "Settings.json.vunknown.bak")]
    [InlineData("../../2.0 beta", "Settings.json.v....2.0beta.bak")]
    [InlineData("1.0-rc_2", "Settings.json.v1.0-rc_2.bak")]
    public void GetBackupPath_sanitizes_the_stored_version(string? storedVersion, string expected)
    {
        Assert.Equal(expected, SettingsFileCodec.GetBackupPath("Settings.json", storedVersion));
    }

    [Fact]
    public void GetBackupPath_limits_the_version_to_32_characters()
    {
        var path = SettingsFileCodec.GetBackupPath("Settings.json", new string('9', 50));

        Assert.Equal($"Settings.json.v{new string('9', 32)}.bak", path);
    }

    [Fact]
    public void PrepareForSave_stamps_the_current_version_first_and_sorts_the_rest()
    {
        var settings = new Dictionary<string, object?>
        {
            ["Zeta"] = 1,
            ["FileVer"] = "1.0",
            ["Alpha"] = true,
            ["Mid"] = "m"
        };

        var prepared = SettingsFileCodec.PrepareForSave(settings);

        Assert.Equal(["FileVer", "Alpha", "Mid", "Zeta"], prepared.Keys);
        Assert.Equal(SettingsFileCodec.SETTINGS_VERSION, prepared["FileVer"]);
        Assert.Equal("1.0", settings["FileVer"]);
    }

    [Fact]
    public void PrepareForSave_adds_the_version_when_missing()
    {
        var prepared = SettingsFileCodec.PrepareForSave(new Dictionary<string, object?> { ["Alpha"] = 1 });

        Assert.Equal(["FileVer", "Alpha"], prepared.Keys);
        Assert.Equal("2.1", prepared["FileVer"]);
    }

    [Fact]
    public void Flatten_keeps_a_decimal_point_only_for_float_settings()
    {
        const string json = """{"Settings":{"FileVer":"2.1","Volume":1,"Scale":1,"Enabled":true,"Name":"abc"}}""";

        var flattened = SettingsFileCodec.Flatten(json, key => key == "Scale");

        Assert.Equal("FileVer/\"2.1\"|Volume/1|Scale/1.0|Enabled/true|Name/\"abc\"", flattened);
    }

    [Fact]
    public void Flatten_uses_invariant_culture_for_floats()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var flattened = SettingsFileCodec.Flatten("""{"Settings":{"Scale":1.5}}""", _ => true);

            Assert.Equal("Scale/1.5", flattened);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Encode_produces_base64_and_Decode_restores_the_settings_types()
    {
        var settings = new Dictionary<string, object?>
        {
            ["FileVer"] = "2.1",
            ["Enabled"] = false,
            ["Name"] = "Hory/Tweaks",
            ["Scale"] = 1f,
            ["Ratio"] = 0.75f,
            ["Volume"] = 3
        };
        var json = JsonSerializer.Serialize(new Dictionary<string, object?> { ["Settings"] = settings });

        var encoded = SettingsFileCodec.Encode(json, key => settings[key] is float);
        var decoded = SettingsFileCodec.Decode(encoded);
        var restored = LoadSettings(decoded);

        Assert.Equal(Convert.ToBase64String(Convert.FromBase64String(encoded)), encoded);
        Assert.Equal("2.1", restored["FileVer"]);
        Assert.Equal(false, restored["Enabled"]);
        Assert.Equal("Hory/Tweaks", restored["Name"]);
        Assert.Equal(1f, restored["Scale"]);
        Assert.Equal(0.75f, restored["Ratio"]);
        Assert.Equal(3, restored["Volume"]);
    }

    [Fact]
    public void Decode_returns_plain_json_unchanged()
    {
        const string json = """{"Settings":{"FileVer":"2.1","Volume":3}}""";

        Assert.Equal(json, SettingsFileCodec.Decode($"  {json}\n"));
    }

    [Fact]
    public void Decode_ignores_malformed_flattened_pairs()
    {
        var decoded = SettingsFileCodec.Decode(SettingsFileCodec.Compress("Volume/3|Broken||Name/\"x\""));

        var restored = LoadSettings(decoded);
        Assert.Equal(2, restored.Count);
        Assert.Equal(3, restored["Volume"]);
        Assert.Equal("x", restored["Name"]);
    }

    [Fact]
    public void Legacy_plain_settings_and_compressed_settings_load_identically()
    {
        var settings = new Dictionary<string, object?>
        {
            ["FileVer"] = "2.0",
            ["Scale"] = 2.5f,
            ["Volume"] = 5
        };
        var plain = JsonSerializer.Serialize(new Dictionary<string, object?> { ["Settings"] = settings });
        var compressed = SettingsFileCodec.Encode(plain, key => settings[key] is float);

        var fromPlain = LoadSettings(SettingsFileCodec.Decode(plain));
        var fromCompressed = LoadSettings(SettingsFileCodec.Decode(compressed));

        Assert.Equal(fromPlain, fromCompressed);
        Assert.True(SettingsFileCodec.RequiresMigration(fromCompressed["FileVer"]!.ToString()));
    }
}
