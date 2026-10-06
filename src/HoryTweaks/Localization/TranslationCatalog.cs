using System.Text.Json;

namespace BetterAmongUs.Localization;

/// <summary>
/// Owns the loaded translation catalogs: the language-name-to-ID lookup and the per-key
/// language maps, plus loading them from the assembly's embedded JSON resources.
/// </summary>
internal static class TranslationCatalog
{
    internal const string ResourcePath = "BetterAmongUs.Resources.Lang";
    internal const string EnglishCatalogName = "en_US";

    internal static Dictionary<string, int> TranslateIdLookup = [];
    internal static Dictionary<string, Dictionary<int, string>> TranslateMaps = [];

    /// <summary>
    /// Loads all language JSON files from the assembly's embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing the language resources.</param>
    internal static void LoadLanguages(System.Reflection.Assembly assembly)
    {
        var jsonFileNames = assembly.GetManifestResourceNames()
            .Where(resourceName => resourceName.StartsWith(ResourcePath) && resourceName.EndsWith(".json"))
            .ToArray();

        TranslateMaps = [];

        if (jsonFileNames.Length == 0)
        {
            BAUPlugin.Logger.Error("JSON translation files do not exist.", "Translator");
            return;
        }

        foreach (var jsonFileName in jsonFileNames)
        {
            LoadLanguageFile(assembly, jsonFileName);
        }

        TranslateIdLookup = TranslateIdLookup.OrderBy(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Loads a single language file from embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing the resource.</param>
    /// <param name="resourceName">The name of the resource file.</param>
    private static void LoadLanguageFile(System.Reflection.Assembly assembly, string resourceName)
    {
        try
        {
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
                return;

            using var reader = new StreamReader(resourceStream);
            var jsonContent = reader.ReadToEnd();
            var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

            if (jsonDictionary == null)
            {
                BAUPlugin.Logger.Error($"Failed to deserialize JSON from {resourceName}", "Translator");
                return;
            }

            if (jsonDictionary.TryGetValue("LanguageID", out var languageIdStr) &&
                int.TryParse(languageIdStr, out var languageId))
            {
                jsonDictionary.Remove("LanguageID");
                var name = resourceName[(ResourcePath.Length + 1)..^5]; // remove path from name
                TranslateIdLookup[name] = languageId;
                MergeTranslations(TranslateMaps, languageId, jsonDictionary);
            }
            else
            {
                BAUPlugin.Logger.Error($"Invalid JSON format in {resourceName}: Missing or invalid 'LanguageID' field.", "Translator");
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error($"Error loading language file {resourceName}: {ex}", "Translator");
        }
    }

    /// <summary>
    /// Merges translations from a language file into the main translation maps.
    /// </summary>
    /// <param name="translationMaps">The main translation dictionary to merge into.</param>
    /// <param name="languageId">The language ID for the translations.</param>
    /// <param name="translations">The translations to merge.</param>
    private static void MergeTranslations(
        Dictionary<string, Dictionary<int, string>> translationMaps,
        int languageId,
        Dictionary<string, string> translations)
    {
        foreach (var (key, value) in translations)
        {
            if (!translationMaps.ContainsKey(key))
            {
                translationMaps[key] = [];
            }

            // Replace escape sequences with actual characters
            var processedValue = value.Replace("\\n", "\n").Replace("\\r", "\r");
            translationMaps[key][languageId] = processedValue;
        }
    }

    /// <summary>
    /// Reports every catalog that lacks keys defined by en_US; those keys render in English at runtime.
    /// </summary>
    internal static void LogEnglishFallbacks()
    {
        if (!TranslateIdLookup.TryGetValue(EnglishCatalogName, out var englishId))
        {
            BAUPlugin.Logger.Error($"{EnglishCatalogName} catalog is missing; untranslated keys cannot fall back to English.", "Translator");
            return;
        }

        foreach (var (name, languageId) in TranslateIdLookup)
        {
            if (languageId == englishId) continue;

            var missingKeys = TranslateMaps
                .Where(kvp => kvp.Value.ContainsKey(englishId) && !kvp.Value.ContainsKey(languageId))
                .Select(kvp => kvp.Key)
                .ToArray();
            if (missingKeys.Length == 0) continue;

            BAUPlugin.Logger.Warning($"{name} is missing {missingKeys.Length} translation(s); English text is used for: {string.Join(", ", missingKeys)}", "Translator");
        }
    }
}
