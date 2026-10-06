using HoryTweaks.Generated;
using HoryTweaks.Utilities;
using System.Globalization;
using System.Text.RegularExpressions;
using HoryTweaks.Infrastructure.Configuration;

namespace HoryTweaks.Localization;

/// <summary>
/// Picks the active language (game, forced system, or console English) and orders the
/// catalog/vanilla/English fallbacks used to resolve a translation key.
/// </summary>
internal static class LanguageSelection
{
    /// <summary>
    /// Gets a translation from the language map with Chinese character detection.
    /// </summary>
    /// <param name="key">The translation key to look up.</param>
    /// <param name="languageId">The target language ID.</param>
    /// <param name="languageMap">The language map containing translations.</param>
    /// <param name="showInvalid">Whether to show invalid key indicators.</param>
    /// <returns>The translated string, the English text when the language lacks the key, or a marked key when English lacks it too.</returns>
    internal static string GetTranslationFromMap(TranslationStrings.TranslationString key, SupportedLangs languageId, Dictionary<int, string> languageMap, bool showInvalid)
    {
        if (languageMap.TryGetValue((int)languageId, out var translation) &&
            !string.IsNullOrEmpty(translation))
        {
            // Check for Chinese characters in non-Chinese languages
            if (!IsChineseLanguage(languageId) && ContainsChineseCharacters(translation))
            {
                var chineseTranslation = Translator.GetString(key, SupportedLangs.SChinese, showInvalid);
                if (translation == chineseTranslation)
                {
                    return GetEnglishFallback(key, showInvalid);
                }
            }
            return translation;
        }

        return languageId == SupportedLangs.English ? $"*{key}" : GetEnglishFallback(key, showInvalid);
    }

    /// <summary>
    /// Fallback method to get vanilla string names.
    /// </summary>
    /// <param name="translationString">The translation key to look up.</param>
    /// <param name="fallbackText">The fallback text to return if not found.</param>
    /// <returns>The translated string or fallback text.</returns>
    internal static string GetVanillaStringFallback(TranslationStrings.TranslationString translationString, string fallbackText)
    {
        var matchingStringNames = EnumUtils.GetAllValues<StringNames>()
            .Where(x => x.ToString() == translationString.Key)
            .ToArray();

        return matchingStringNames.Length > 0 ? Translator.GetString(matchingStringNames[0]) : fallbackText;
    }

    /// <summary>
    /// Gets the target language ID based on settings and system configuration.
    /// </summary>
    /// <param name="useConsoleLanguage">Whether to force English for console.</param>
    /// <returns>The target language ID.</returns>
    internal static SupportedLangs GetTargetLanguageId(bool useConsoleLanguage = false)
    {
        if (useConsoleLanguage) return SupportedLangs.English;
        if (BAUConfigs.ForceOwnLanguage.Value) return GetUserSystemLanguage();

        return TranslationController.InstanceExists ?
            TranslationController.Instance.currentLanguage.languageID :
            SupportedLangs.English;
    }

    /// <summary>
    /// Gets the user's system language as a SupportedLangs enum.
    /// </summary>
    /// <returns>The system language ID.</returns>
    internal static SupportedLangs GetUserSystemLanguage()
    {
        try
        {
            var cultureName = CultureInfo.CurrentUICulture.Name;

            return cultureName switch
            {
                string name when name.StartsWith("zh_CHT") => SupportedLangs.TChinese,
                string name when name.StartsWith("zh-Hant") || name.StartsWith("zh_Hant") => SupportedLangs.TChinese,
                string name when name.StartsWith("zh") => SupportedLangs.SChinese,
                string name when name.StartsWith("ru") => SupportedLangs.Russian,
                string name when name.StartsWith("en") => SupportedLangs.English,
                string name when name.StartsWith("pt-BR") || name.StartsWith("pt_BR") => SupportedLangs.Brazilian,
                string name when name.StartsWith("pt") => SupportedLangs.Portuguese,
                string name when name.StartsWith("es-ES") || name.StartsWith("es_ES") => SupportedLangs.Spanish,
                string name when name.StartsWith("es") => SupportedLangs.Latam,
                string name when name.StartsWith("de") => SupportedLangs.German,
                string name when name.StartsWith("fr") => SupportedLangs.French,
                string name when name.StartsWith("it") => SupportedLangs.Italian,
                string name when name.StartsWith("ja") => SupportedLangs.Japanese,
                string name when name.StartsWith("ko") => SupportedLangs.Korean,
                string name when name.StartsWith("nl") => SupportedLangs.Dutch,
                string name when name.StartsWith("fil") || name.StartsWith("tl") => SupportedLangs.Filipino,
                string name when name.StartsWith("ga") => SupportedLangs.Irish,
                _ => TranslationController.Instance.currentLanguage.languageID
            };
        }
        catch
        {
            return SupportedLangs.English;
        }
    }

    /// <summary>
    /// Gets the English translation used whenever the active language lacks a key.
    /// </summary>
    /// <param name="key">The translation key to look up.</param>
    /// <param name="showInvalid">Whether to show invalid key indicators.</param>
    /// <returns>The English translation of the key.</returns>
    private static string GetEnglishFallback(TranslationStrings.TranslationString key, bool showInvalid = true) =>
        Translator.GetString(key, SupportedLangs.English, showInvalid);

    /// <summary>
    /// Checks if a language ID represents a Chinese language.
    /// </summary>
    /// <param name="languageId">The language ID to check.</param>
    /// <returns>true if the language is Chinese; otherwise, false.</returns>
    private static bool IsChineseLanguage(SupportedLangs languageId) =>
        languageId is SupportedLangs.SChinese or SupportedLangs.TChinese;

    /// <summary>
    /// Checks if a string contains Chinese characters.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>true if Chinese characters are found; otherwise, false.</returns>
    private static bool ContainsChineseCharacters(string text) =>
        Regex.IsMatch(text, @"[\u4e00-\u9fa5]");
}
