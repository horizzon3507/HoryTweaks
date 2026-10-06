using BetterAmongUs.Core.Localization;
using System.Text.Json;
using Xunit;
using BetterAmongUs.Core.Localization;

namespace HoryTweaks.Tests.Localization;

/// <summary>
/// Exercises the in-process translation catalog validator with the fixture cases that used
/// to run through ValidateTranslations.ps1/ValidateTranslations.Tests.ps1.
/// </summary>
public class TranslationValidationTests
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["LanguageID"] = "0",
        ["Chat.Welcome"] = "Welcome {0}",
        ["Chat.Kicked"] = "{0} kicked {1} after {2} warnings",
        ["Menu.Title"] = "HoryTweaks"
    };

    private static readonly Dictionary<string, string> ShippedLanguageIds = new()
    {
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
        ["ga_IE"] = "15"
    };

    private static Dictionary<string, string> FixtureCatalogs()
    {
        var catalogs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en_US"] = JsonSerializer.Serialize(English)
        };
        foreach (var (name, languageId) in ShippedLanguageIds)
        {
            var catalog = new Dictionary<string, string>(English) { ["LanguageID"] = languageId };
            catalogs[name] = JsonSerializer.Serialize(catalog);
        }
        return catalogs;
    }

    private static void Mutate(Dictionary<string, string> catalogs, string name, Action<Dictionary<string, string>> change)
    {
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(catalogs[name])!;
        change(catalog);
        catalogs[name] = JsonSerializer.Serialize(catalog);
    }

    private static IEnumerable<TranslationIssue> Validate(Dictionary<string, string> catalogs) =>
        TranslationCatalogValidator.Validate(catalogs);

    [Fact]
    public void Repository_catalogs_pass_validation()
    {
        var directory = Path.Combine(RepositoryPaths.Src, "Resources", "Lang");
        var catalogs = Directory.EnumerateFiles(directory, "*.json")
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), File.ReadAllText, StringComparer.Ordinal);

        var issues = Validate(catalogs).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Matching_catalogs_pass()
    {
        var issues = Validate(FixtureCatalogs()).ToList();
        Assert.Empty(issues);
    }

    [Fact]
    public void Missing_shipped_catalog_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        catalogs.Remove("fr_FR");

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "missing-catalog"));
        Assert.Equal("fr_FR", issue.Catalog);
    }

    [Fact]
    public void Stub_catalog_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        catalogs["fr_FR"] = JsonSerializer.Serialize(new Dictionary<string, string> { ["LanguageID"] = "8" });

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "stub-catalog"));
        Assert.Equal("fr_FR", issue.Catalog);
    }

    [Theory]
    [InlineData("{0} kicked {1} after {3} warnings")]
    [InlineData("{0} kicked {1}")]
    [InlineData("{0} kicked {1} after {2} warnings {2}")]
    [InlineData("{0} kicked {1} after {a} warnings")]
    public void Placeholder_differences_fail_validation(string translated)
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "pt_BR", catalog => catalog["Chat.Kicked"] = translated);

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "placeholder-mismatch"));
        Assert.Equal("pt_BR", issue.Catalog);
        Assert.Equal("Chat.Kicked", issue.Key);
    }

    [Fact]
    public void Reordered_placeholders_are_accepted()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "pt_BR", catalog => catalog["Chat.Kicked"] = "após {2} avisos, {1} foi expulso por {0}");

        var issues = Validate(catalogs).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Missing_and_extra_keys_fail_validation()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "pt_BR", catalog =>
        {
            catalog.Remove("Menu.Title");
            catalog["Menu.Titel"] = "HoryTweaks";
        });

        var issues = Validate(catalogs).ToList();

        var missing = Assert.Single(issues.Where(i => i.Code == "missing-key"));
        Assert.Equal("pt_BR", missing.Catalog);
        Assert.Equal("Menu.Title", missing.Key);
        var extra = Assert.Single(issues.Where(i => i.Code == "extra-key"));
        Assert.Equal("pt_BR", extra.Catalog);
        Assert.Equal("Menu.Titel", extra.Key);
    }

    [Fact]
    public void Wrong_language_id_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "pt_BR", catalog => catalog["LanguageID"] = "3");

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "wrong-language-id"));
        Assert.Equal("pt_BR", issue.Catalog);
    }

    [Fact]
    public void Unknown_non_stub_language_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        catalogs["xx_XX"] = JsonSerializer.Serialize(new Dictionary<string, string>(English) { ["LanguageID"] = "99" });

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "unknown-catalog"));
        Assert.Equal("xx_XX", issue.Catalog);
    }

    [Fact]
    public void Catalog_without_language_id_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "pt_BR", catalog => catalog.Remove("LanguageID"));

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "missing-language-id"));
        Assert.Equal("pt_BR", issue.Catalog);
    }

    [Fact]
    public void Invalid_json_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        catalogs["pt_BR"] = "{ \"LanguageID\": \"2\", ";

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "invalid-json"));
        Assert.Equal("pt_BR", issue.Catalog);
    }

    [Fact]
    public void Empty_translation_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "it_IT", catalog => catalog["Menu.Title"] = "");

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "empty-value"));
        Assert.Equal("it_IT", issue.Catalog);
        Assert.Equal("Menu.Title", issue.Key);
    }

    [Fact]
    public void Broken_rich_text_tag_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "ru_RU", catalog => catalog["Menu.Title"] = "<color=#4f92ff>HoryTweaks</colour>");

        var issues = Validate(catalogs).ToList();

        var issue = Assert.Single(issues.Where(i => i.Code == "rich-text-mismatch"));
        Assert.Equal("ru_RU", issue.Catalog);
        Assert.Equal("Menu.Title", issue.Key);
    }

    [Fact]
    public void Translated_command_token_fails_validation()
    {
        var catalogs = FixtureCatalogs();
        Mutate(catalogs, "en_US", catalog => catalog["Chat.Welcome"] = "Run /commands for help");
        Mutate(catalogs, "fil_PH", catalog => catalog["Chat.Welcome"] = "Run /mgautos for help");

        var issues = Validate(catalogs).ToList();

        // Catalogs still holding the English text all report the new token; the translated
        // one reports the divergence on the key that changed.
        var translated = Assert.Single(issues.Where(i => i.Code == "command-token-mismatch" && i.Catalog == "fil_PH"));
        Assert.Equal("Chat.Welcome", translated.Key);
        Assert.DoesNotContain(issues, i => i.Code == "command-token-mismatch" && i.Catalog == "en_US");
    }

    [Fact]
    public void Numeric_language_id_is_normalized_to_string()
    {
        var catalogs = FixtureCatalogs();
        catalogs["pt_BR"] = "{ \"LanguageID\": 2, " +
            string.Join(",", English.Where(k => k.Key != "LanguageID").Select(k => $"\"{k.Key}\": \"{k.Value}\"")) + "}";

        var issues = Validate(catalogs).ToList();

        Assert.Empty(issues);
    }
}
