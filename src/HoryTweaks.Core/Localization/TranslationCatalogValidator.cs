using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoryTweaks.Core.Localization;

/// <summary>
/// Validates translation catalogs against the English reference catalog.
/// Reimplements the rules previously enforced by build/ValidateTranslations.ps1 so the
/// check runs inside the .NET test suite instead of a PowerShell step.
/// </summary>
internal static class TranslationCatalogValidator
{
    /// <summary>Expected catalog name → LanguageID (as string), matching the game language ids.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ExpectedLanguageIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en_US"] = "0",
            ["es_419"] = "1",
            ["pt_BR"] = "2",
            ["pt_PT"] = "3",
            ["ko_KR"] = "4",
            ["ru_RU"] = "5",
            ["nl_NL"] = "6",
            ["fil_PH"] = "7",
            ["fr_FR"] = "8",
            ["de_DE"] = "9",
            ["it_IT"] = "10",
            ["ja_JP"] = "11",
            ["es_ES"] = "12",
            ["zh_CN"] = "13",
            ["zh_TW"] = "14",
            ["ga_IE"] = "15",
        };

    private const string EnglishCatalog = "en_US";
    private const string LanguageIdKey = "LanguageID";

    private static readonly Regex PlaceholderRegex = new(@"\{\d+\}", RegexOptions.Compiled);
    private static readonly Regex RichTextRegex = new(@"</?[A-Za-z#][^<>]*>", RegexOptions.Compiled);
    private static readonly Regex CommandTokenRegex = new(@"(?<![\w/])/[A-Za-z][\w-]*", RegexOptions.Compiled);

    /// <summary>
    /// Validates the given catalogs. Keys are catalog names (file name without extension),
    /// values are the raw JSON contents. Returns every issue found; an empty list means valid.
    /// </summary>
    internal static IReadOnlyList<TranslationIssue> Validate(IReadOnlyDictionary<string, string> jsonByCatalog)
    {
        var issues = new List<TranslationIssue>();

        var parsed = new Dictionary<string, IReadOnlyDictionary<string, string>?>(StringComparer.Ordinal);
        foreach (var (name, json) in jsonByCatalog)
        {
            parsed[name] = ParseCatalog(json, name, issues);
        }

        // Missing expected catalogs.
        foreach (var expected in ExpectedLanguageIds.Keys)
        {
            if (!parsed.ContainsKey(expected))
            {
                issues.Add(new TranslationIssue("missing-catalog", expected, null));
            }
        }

        if (!parsed.TryGetValue(EnglishCatalog, out var english) || english is null)
        {
            // Without a valid English reference nothing else can be compared.
            if (!jsonByCatalog.ContainsKey(EnglishCatalog))
            {
                // missing-catalog already reported above.
                return issues;
            }
            return issues;
        }

        var englishIssues = ValidateCatalogBasics(EnglishCatalog, english, issues);
        var englishKeys = english.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        if (englishKeys.Length <= 1)
        {
            issues.Add(new TranslationIssue("stub-catalog", EnglishCatalog, null));
        }

        var englishValid = !englishIssues;

        foreach (var (name, catalog) in parsed.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (catalog is null || name == EnglishCatalog)
            {
                continue;
            }

            if (ValidateCatalogBasics(name, catalog, issues) || !englishValid)
            {
                continue;
            }

            // Key-set parity (ordinal, case-sensitive).
            var catalogKeys = catalog.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            foreach (var key in englishKeys)
            {
                if (!catalog.ContainsKey(key))
                {
                    issues.Add(new TranslationIssue("missing-key", name, key));
                }
            }
            foreach (var key in catalogKeys)
            {
                if (!english.ContainsKey(key))
                {
                    issues.Add(new TranslationIssue("extra-key", name, key));
                }
            }

            foreach (var key in englishKeys)
            {
                if (!catalog.TryGetValue(key, out var value))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    issues.Add(new TranslationIssue("empty-value", name, key));
                }

                var englishValue = english[key];
                CompareTokens("placeholder-mismatch", PlaceholderRegex, englishValue, value, name, key, issues);
                CompareTokens("rich-text-mismatch", RichTextRegex, englishValue, value, name, key, issues);
                CompareTokens("command-token-mismatch", CommandTokenRegex, englishValue, value, name, key, issues);
            }
        }

        return issues;
    }

    /// <summary>
    /// Checks LanguageID presence/value and the LanguageID-only stub rule.
    /// Returns true when the catalog failed a gate and must not be compared further.
    /// </summary>
    private static bool ValidateCatalogBasics(string name, IReadOnlyDictionary<string, string> catalog, List<TranslationIssue> issues)
    {
        var failed = false;

        if (!catalog.TryGetValue(LanguageIdKey, out var languageId) || string.IsNullOrWhiteSpace(languageId))
        {
            issues.Add(new TranslationIssue("missing-language-id", name, LanguageIdKey));
            failed = true;
        }

        if (catalog.Count == 1)
        {
            issues.Add(new TranslationIssue("stub-catalog", name, null));
            return true;
        }

        if (!ExpectedLanguageIds.TryGetValue(name, out var expectedId))
        {
            issues.Add(new TranslationIssue("unknown-catalog", name, null));
            return true;
        }

        if (!failed && !string.Equals(languageId, expectedId, StringComparison.Ordinal))
        {
            issues.Add(new TranslationIssue("wrong-language-id", name, LanguageIdKey));
        }

        return failed;
    }

    private static IReadOnlyDictionary<string, string>? ParseCatalog(string json, string name, List<TranslationIssue> issues)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new TranslationIssue("invalid-json", name, null));
                return null;
            }

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                entries[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.GetRawText(),
                    _ => string.Empty,
                };
            }
            return entries;
        }
        catch (JsonException)
        {
            issues.Add(new TranslationIssue("invalid-json", name, null));
            return null;
        }
    }

    private static void CompareTokens(string code, Regex regex, string expected, string actual,
        string catalog, string key, List<TranslationIssue> issues)
    {
        var expectedTokens = SortedTokens(regex, expected);
        var actualTokens = SortedTokens(regex, actual);
        if (!expectedTokens.SequenceEqual(actualTokens, StringComparer.Ordinal))
        {
            issues.Add(new TranslationIssue(code, catalog, key));
        }
    }

    private static List<string> SortedTokens(Regex regex, string value)
    {
        var tokens = regex.Matches(value).Select(m => m.Value).ToList();
        tokens.Sort(StringComparer.Ordinal);
        return tokens;
    }
}

/// <summary>A single validation failure in a translation catalog.</summary>
internal sealed record TranslationIssue(string Code, string Catalog, string? Key);
