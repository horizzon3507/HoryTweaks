using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace BetterAmongUs.Data.Json;

/// <summary>
/// Pure helpers for the settings file format: value conversion, version migration,
/// key ordering and the flattened GZIP/Base64 representation used when compression is enabled.
/// </summary>
internal static class SettingsFileCodec
{
    /// <summary>
    /// The current version identifier for the settings file format.
    /// </summary>
    internal const string SETTINGS_VERSION = "2.1";

    /// <summary>
    /// The dictionary key used to store the settings file version.
    /// </summary>
    internal const string SETTINGS_VERSION_KEY = "FileVer";

    /// <summary>
    /// The JSON property that wraps the settings dictionary.
    /// </summary>
    internal const string SETTINGS_PROPERTY = "Settings";

    private const char PAIR_SEPARATOR = '|';
    private const char KEY_VALUE_SEPARATOR = '/';
    private const string FLOAT_FORMAT = "0.0########";

    /// <summary>
    /// Converts a deserialized JSON element to the primitive type stored in the settings dictionary.
    /// </summary>
    /// <exception cref="NotSupportedException">The JSON value kind is not a supported setting type.</exception>
    internal static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out int intValue) => intValue,
            JsonValueKind.Number when element.TryGetSingle(out float floatValue) => floatValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            _ => throw new NotSupportedException($"Unsupported JSON type: {element.ValueKind}")
        };
    }

    /// <summary>
    /// Determines whether a stored file version requires a backup and migration to the current version.
    /// </summary>
    internal static bool RequiresMigration(string? storedVersion)
    {
        return storedVersion != SETTINGS_VERSION;
    }

    /// <summary>
    /// Builds the backup path for a settings file that stored the given version.
    /// </summary>
    internal static string GetBackupPath(string filePath, string? storedVersion)
    {
        var version = new string((storedVersion ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            .Take(32)
            .ToArray());
        if (string.IsNullOrWhiteSpace(version))
            version = "unknown";

        return $"{filePath}.v{version}.bak";
    }

    /// <summary>
    /// Stamps the current version and orders the entries so the version key is written first.
    /// </summary>
    internal static Dictionary<string, object?> PrepareForSave(IReadOnlyDictionary<string, object?> settings)
    {
        var stamped = new Dictionary<string, object?>(settings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        {
            [SETTINGS_VERSION_KEY] = SETTINGS_VERSION
        };
        return stamped.OrderBy(kvp => kvp.Key != SETTINGS_VERSION_KEY)
                      .ThenBy(kvp => kvp.Key)
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Flattens the serialized settings JSON into <c>key/value|key/value</c> pairs.
    /// Float settings keep a decimal point so they are restored as floats.
    /// </summary>
    /// <param name="json">The serialized settings file JSON.</param>
    /// <param name="isFloatSetting">Returns true when the in-memory value for the key is a float.</param>
    internal static string Flatten(string json, Func<string, bool> isFloatSetting)
    {
        using var jsonDoc = JsonDocument.Parse(json);
        var settingsDict = jsonDoc.RootElement.GetProperty(SETTINGS_PROPERTY);
        var sb = new StringBuilder();

        foreach (var kvp in settingsDict.EnumerateObject())
        {
            if (sb.Length > 0) sb.Append(PAIR_SEPARATOR);
            sb.Append(kvp.Name).Append(KEY_VALUE_SEPARATOR);

            if (kvp.Value.ValueKind == JsonValueKind.Number &&
                kvp.Value.TryGetSingle(out float floatValue) &&
                isFloatSetting(kvp.Name))
            {
                sb.Append(floatValue.ToString(FLOAT_FORMAT, CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(kvp.Value.GetRawText());
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Rebuilds the settings file JSON from flattened <c>key/value|key/value</c> pairs.
    /// </summary>
    internal static string Expand(string flattened)
    {
        var settingsDict = new Dictionary<string, object?>();
        foreach (var pair in flattened.Split(PAIR_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(KEY_VALUE_SEPARATOR, 2);
            if (parts.Length == 2)
            {
                using var doc = JsonDocument.Parse(parts[1]);
                settingsDict[parts[0]] = doc.RootElement.Clone();
            }
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?> { [SETTINGS_PROPERTY] = settingsDict });
    }

    /// <summary>
    /// Compresses flattened settings with GZIP and encodes them as Base64.
    /// </summary>
    internal static string Compress(string flattened)
    {
        byte[] flattenedData = Encoding.UTF8.GetBytes(flattened);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
        {
            gzip.Write(flattenedData, 0, flattenedData.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// Decodes Base64 GZIP content back to flattened settings.
    /// </summary>
    /// <returns>False when <paramref name="text"/> is not Base64, which means it is plain JSON.</returns>
    internal static bool TryDecompress(string text, out string flattened)
    {
        byte[] compressedBytes = new byte[text.Length];
        if (!Convert.TryFromBase64String(text, compressedBytes, out var bytesWritten))
        {
            flattened = string.Empty;
            return false;
        }
        Array.Resize(ref compressedBytes, bytesWritten);

        using var ms = new MemoryStream(compressedBytes);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        flattened = reader.ReadToEnd();
        return true;
    }

    /// <summary>
    /// Encodes settings file JSON into the compressed on-disk representation.
    /// </summary>
    internal static string Encode(string json, Func<string, bool> isFloatSetting)
    {
        return Compress(Flatten(json, isFloatSetting));
    }

    /// <summary>
    /// Decodes on-disk settings text, accepting both compressed and plain JSON content.
    /// </summary>
    internal static string Decode(string text)
    {
        text = text.Trim();
        return TryDecompress(text, out var flattened) ? Expand(flattened) : text;
    }
}
