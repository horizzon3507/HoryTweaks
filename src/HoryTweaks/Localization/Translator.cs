using HoryTweaks.Generated;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HoryTweaks.Infrastructure.Configuration;

namespace HoryTweaks.Localization;

/// <summary>
/// Provides translation services for BetterAmongUs, supporting multiple languages and fallback mechanisms.
/// </summary>
internal static class Translator
{
    private static bool _loggedActiveLanguage;

    /// <summary>
    /// Initializes the translator by loading all language files from embedded resources.
    /// </summary>
    internal static void Initialize()
    {
        BAUPlugin.Logger.Log("Loading language files...", "Translator");
        LoadLanguages();
        BAUPlugin.Logger.Log($"Language files loaded successfully: {string.Join(", ", TranslationCatalog.TranslateIdLookup.Select(kvp => $"{kvp.Key}={kvp.Value}"))}", "Translator");
        TranslationCatalog.LogEnglishFallbacks();
    }

    /// <summary>
    /// Loads all language JSON files from the assembly's embedded resources.
    /// </summary>
    private static void LoadLanguages()
    {
        try
        {
            TranslationCatalog.LoadLanguages(BAUPlugin.ModInfo.Assembly);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error($"Error loading languages: {ex}", "Translator");
        }
    }

    /// <summary>
    /// Gets a translated string for a specific language.
    /// </summary>
    /// <param name="translationString">The translation key.</param>
    /// <param name="languageId">The language to use.</param>
    /// <param name="showInvalid">Whether to show invalid key indicators.</param>
    /// <returns>The translated string.</returns>
    internal static string GetString(TranslationStrings.TranslationString translationString, SupportedLangs languageId, bool showInvalid = true)
    {
        var fallbackText = showInvalid ? $"<INVALID:{translationString.Key}>" : translationString.Key;

        try
        {
            // Try to get from custom translations
            if (TranslationCatalog.TranslateMaps.TryGetValue(translationString.Key, out var languageMap))
            {
                var result = LanguageSelection.GetTranslationFromMap(translationString, languageId, languageMap, showInvalid);
                if (result != null) return result;
            }

            // Fallback to vanilla string names
            return LanguageSelection.GetVanillaStringFallback(translationString, fallbackText);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error($"Error retrieving string [{translationString}]: {ex}", "Translator");
            return fallbackText;
        }
    }

    /// <summary>
    /// Retrieves a localized string corresponding to the specified key, with optional formatting and retrieval options.
    /// </summary>
    /// <param name="translationString">The key that identifies the string resource to retrieve.</param>
    /// <param name="formatting">An array of objects to format the retrieved string with, or null to return the string without formatting.</param>
    /// <param name="useConsoleLanguage">true to force retrieval in English for console output; otherwise, false.</param>
    /// <param name="showInvalid">true to return a placeholder for invalid or missing keys; otherwise, false to return the key itself.</param>
    /// <param name="vanilla">true to retrieve the string using the default (vanilla) translation set; otherwise, false to use the current or
    /// specified language.</param>
    /// <returns>The localized string corresponding to the specified key, formatted if formatting is provided. Returns a
    /// placeholder or the key itself if the key is invalid, depending on the showInvalid parameter.</returns>
    internal static string GetString(TranslationStrings.TranslationString translationString, string[]? formatting = null, bool useConsoleLanguage = false, bool showInvalid = true, bool vanilla = false)
    {
        if (vanilla)
        {
            string nameToFind = translationString.Key;
            if (Enum.TryParse(nameToFind, out StringNames text))
            {
                return TranslationController.Instance.GetString(text);
            }
            else
            {
                return showInvalid ? $"<INVALID:{nameToFind}> (vanillaStr)" : nameToFind;
            }
        }
        var langId = TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage.languageID : SupportedLangs.English;
        if (useConsoleLanguage) langId = SupportedLangs.English;
        if (BAUConfigs.ForceOwnLanguage.Value) langId = LanguageSelection.GetUserSystemLanguage();
        if (!_loggedActiveLanguage && TranslationController.InstanceExists && !useConsoleLanguage)
        {
            _loggedActiveLanguage = true;
            BAUPlugin.Logger.Log($"Active language: {langId} ({(int)langId}), forced by system language: {BAUConfigs.ForceOwnLanguage.Value}", "Translator");
        }
        string str = GetString(translationString, langId, showInvalid);
        if (formatting != null)
            str = string.Format(str, formatting);
        return str ?? string.Empty;
    }

    /// <summary>
    /// Gets a vanilla Among Us string by StringNames enum.
    /// </summary>
    /// <param name="stringName">The StringNames enum value.</param>
    /// <returns>The translated string.</returns>
    internal static string GetString(StringNames stringName) =>
        TranslationController.Instance.GetString(stringName, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
}
