using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoryTweaks.Core.Presets;

/// <summary>
/// Encodes preset settings into a compact share code and decodes them back with strict validation.
/// </summary>
/// <remarks>
/// Codes look like <c>HTP1:&lt;base64 gzip json&gt;</c>. Only flat objects with primitive values
/// are accepted, sizes are bounded and keys are restricted to setting identifiers.
/// </remarks>
internal static class PresetShareCodec
{
    internal const string Prefix = "HTP1:";
    internal const int MaxSettings = 256;
    private const int MaxEncodedLength = 32 * 1024;
    private const int MaxDecodedBytes = 64 * 1024;

    private static readonly Regex SettingKeyPattern = new("^[A-Za-z0-9_]{1,64}$", RegexOptions.Compiled);

    internal static bool IsSupportedValue(object? value) =>
        value is bool or int or float or string;

    internal static bool IsValidKey(string key) => SettingKeyPattern.IsMatch(key);

    /// <summary>
    /// Builds a share code from setting values. Unsupported values and invalid keys are skipped.
    /// </summary>
    internal static string Encode(IEnumerable<KeyValuePair<string, object?>> settings)
    {
        var payload = new Dictionary<string, object?>();
        foreach (var (key, value) in settings)
        {
            if (IsValidKey(key) && IsSupportedValue(value))
                payload[key] = value;
        }

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(json);
        }

        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    /// <summary>
    /// Parses a share code. Returns false for anything that is not a well formed code.
    /// </summary>
    internal static bool TryDecode(string? text, out Dictionary<string, object?> settings)
    {
        settings = [];
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.Length > MaxEncodedLength || !text.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        try
        {
            byte[] compressed = Convert.FromBase64String(text[Prefix.Length..]);
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            byte[] buffer = new byte[4096];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > MaxDecodedBytes)
                    return false;
            }

            using var document = JsonDocument.Parse(output.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (settings.Count >= MaxSettings || !IsValidKey(property.Name))
                    return false;

                if (!TryConvert(property.Value, out object? value))
                    return false;

                settings[property.Name] = value;
            }

            return settings.Count > 0;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException or JsonException)
        {
            return false;
        }
    }

    private static bool TryConvert(JsonElement element, out object? value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                value = element.GetBoolean();
                return true;
            case JsonValueKind.Number when element.TryGetInt32(out int intValue):
                value = intValue;
                return true;
            case JsonValueKind.Number when element.TryGetSingle(out float floatValue) && float.IsFinite(floatValue):
                value = floatValue;
                return true;
            case JsonValueKind.String:
                value = element.GetString();
                return true;
            default:
                value = null;
                return false;
        }
    }
}
