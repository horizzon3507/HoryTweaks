using System.Text.Json;
using BetterAmongUs.Core.Presets;
using Xunit;

namespace HoryTweaks.Tests.Presets;

public class SettingsSectionScannerTests
{
    private static List<(string Key, string Value)> Sections(string flattened)
        => SettingsSectionScanner.Scan(flattened)
            .Select(s => (s.Key(flattened).ToString(), s.Value(flattened).ToString()))
            .ToList();

    [Fact]
    public void Scan_yields_each_key_value_pair()
    {
        Assert.Equal(
            [("A", "1"), ("B", "\"two\"")],
            Sections("A/1|B/\"two\""));
    }

    [Fact]
    public void Scan_ignores_pipe_inside_quoted_values()
    {
        Assert.Equal(
            [("Name", "\"a|b\""), ("Next", "2")],
            Sections("Name/\"a|b\"|Next/2"));
    }

    [Fact]
    public void Scan_ignores_pipe_after_an_escaped_quote()
    {
        Assert.Equal(
            [("K", "\"a\\\"|b\""), ("D", "1")],
            Sections("K/\"a\\\"|b\"|D/1"));
    }

    [Fact]
    public void Scan_uses_the_first_slash_outside_strings_as_separator()
    {
        Assert.Equal(
            [("K", "\"x/y\""), ("J", "/z")],
            Sections("K/\"x/y\"|J//z"));
    }

    [Fact]
    public void Scan_skips_segments_without_a_separator()
    {
        Assert.Equal(
            [("A", "1"), ("B", "2")],
            Sections("A/1|junk|B/2"));
    }

    [Fact]
    public void Scan_skips_empty_segments()
    {
        Assert.Equal(
            [("A", "1")],
            Sections("|A/1|"));
    }

    [Fact]
    public void Scan_empty_input_yields_nothing()
    {
        Assert.Empty(Sections(string.Empty));
    }

    private static Dictionary<string, object?> LoadSettings(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty(SettingsFileCodec.SETTINGS_PROPERTY)
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => SettingsFileCodec.ConvertElement(p.Value));
    }

    [Fact]
    public void Expand_roundtrips_values_containing_pipes_slashes_quotes_and_unicode()
    {
        const string json = """{"Settings":{"PresetName":"a|b/c\"d","Note":"café ☕","Volume":3}}""";

        var flattened = SettingsFileCodec.Flatten(json, _ => false);
        var restored = LoadSettings(SettingsFileCodec.Expand(flattened));

        Assert.Equal("a|b/c\"d", restored["PresetName"]);
        Assert.Equal("café ☕", restored["Note"]);
        Assert.Equal(3, restored["Volume"]);
    }

    [Fact]
    public void Encode_and_decode_roundtrip_a_preset_name_with_separators()
    {
        const string json = """{"Settings":{"PresetName":"a|b/c\"d","Scale":1.5}}""";

        var encoded = SettingsFileCodec.Encode(json, key => key == "Scale");
        var restored = LoadSettings(SettingsFileCodec.Decode(encoded));

        Assert.Equal("a|b/c\"d", restored["PresetName"]);
        Assert.Equal(1.5f, restored["Scale"]);
    }
}
