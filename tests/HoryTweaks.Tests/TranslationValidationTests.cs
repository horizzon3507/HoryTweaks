using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HoryTweaks.Tests;

/// <summary>
/// Exercises src/build/ValidateTranslations.ps1 against fixture catalogs so the CI gate itself is tested.
/// </summary>
public class TranslationValidationTests : IDisposable
{
    private static readonly string ScriptPath = Path.Combine(RepositoryPaths.Src, "build", "ValidateTranslations.ps1");

    private static readonly Dictionary<string, string> English = new()
    {
        ["LanguageID"] = "0",
        ["Chat.Welcome"] = "Welcome {0}",
        ["Chat.Kicked"] = "{0} kicked {1} after {2} warnings",
        ["Menu.Title"] = "HoryTweaks"
    };

    /// <summary>
    /// Every catalog the validator expects to ship, mirroring $expectedLanguageIds in the script.
    /// </summary>
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

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HoryTweaks.Tests", Guid.NewGuid().ToString("N"));

    public TranslationValidationTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private void WriteCatalog(string name, IReadOnlyDictionary<string, string> entries)
    {
        File.WriteAllText(Path.Combine(_directory, $"{name}.json"), JsonSerializer.Serialize(entries));
    }

    private void WriteRaw(string name, string content)
    {
        File.WriteAllText(Path.Combine(_directory, $"{name}.json"), content);
    }

    private static Dictionary<string, string> Translation(string languageId, Action<Dictionary<string, string>>? mutate = null)
    {
        var catalog = English.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        catalog["LanguageID"] = languageId;
        mutate?.Invoke(catalog);
        return catalog;
    }

    /// <summary>
    /// Writes en_US plus a complete copy for every shipped language, then lets a test overwrite one of them.
    /// </summary>
    private void WriteShippedCatalogs()
    {
        WriteCatalog("en_US", English);
        foreach (var (name, languageId) in ShippedLanguageIds)
        {
            WriteCatalog(name, Translation(languageId));
        }
    }

    private static (int ExitCode, string Output) RunValidator(string? languageDirectory)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryPaths.Root
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(ScriptPath);
        if (languageDirectory != null)
        {
            startInfo.ArgumentList.Add("-LanguageDirectory");
            startInfo.ArgumentList.Add(languageDirectory);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("pwsh is required to run the translation validation tests.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "ValidateTranslations.ps1 did not finish within two minutes.");

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private (int ExitCode, string Output) RunValidatorOnFixtures()
    {
        return RunValidator(_directory);
    }

    [Fact]
    public void Repository_catalogs_pass_validation()
    {
        var (exitCode, output) = RunValidator(null);

        Assert.True(exitCode == 0, output);
        Assert.Contains("Validated", output);
    }

    [Fact]
    public void Matching_catalogs_pass()
    {
        WriteShippedCatalogs();
        WriteCatalog("pt_BR", Translation("2", catalog =>
        {
            catalog["Chat.Welcome"] = "Bem-vindo {0}";
            catalog["Chat.Kicked"] = "{1} foi expulso por {0} após {2} avisos";
        }));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.True(exitCode == 0, output);
        Assert.Contains("Validated 16 language catalogs with 3 translation keys", output);
        Assert.Contains("skipped 0 allowed stub catalogs", output);
    }

    [Fact]
    public void Missing_shipped_catalog_fails_validation()
    {
        WriteShippedCatalogs();
        File.Delete(Path.Combine(_directory, "fr_FR.json"));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Expected language catalogs are missing from", output);
        Assert.Contains("[fr_FR]", output);
    }

    [Fact]
    public void Stub_catalog_fails_validation()
    {
        WriteShippedCatalogs();
        WriteCatalog("fr_FR", new Dictionary<string, string> { ["LanguageID"] = "8" });

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("fr_FR is advertised as a complete catalog but only contains LanguageID", output);
    }

    [Theory]
    [InlineData("{0} kicked {1} after {3} warnings")]
    [InlineData("{0} kicked {1}")]
    [InlineData("{0} kicked {1} after {2} warnings {2}")]
    [InlineData("{0} kicked {1} after {a} warnings")]
    public void Placeholder_differences_fail_validation(string translated)
    {
        WriteShippedCatalogs();
        WriteCatalog("pt_BR", Translation("2", catalog => catalog["Chat.Kicked"] = translated));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Placeholder mismatch for 'Chat.Kicked' in pt_BR", output);
    }

    [Fact]
    public void Reordered_placeholders_are_accepted()
    {
        WriteShippedCatalogs();
        WriteCatalog("pt_BR", Translation("2", catalog => catalog["Chat.Kicked"] = "após {2} avisos, {1} foi expulso por {0}"));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.True(exitCode == 0, output);
    }

    [Fact]
    public void Missing_and_extra_keys_fail_validation()
    {
        WriteShippedCatalogs();
        WriteCatalog("pt_BR", Translation("2", catalog =>
        {
            catalog.Remove("Menu.Title");
            catalog["Menu.Titel"] = "HoryTweaks";
        }));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("pt_BR translation keys differ. Missing: [Menu.Title]; extra: [Menu.Titel]", output);
    }

    [Fact]
    public void Wrong_language_id_fails_validation()
    {
        WriteShippedCatalogs();
        WriteCatalog("pt_BR", Translation("3"));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("pt_BR LanguageID must be the string '2'", output);
    }

    [Fact]
    public void Unknown_non_stub_language_fails_validation()
    {
        WriteShippedCatalogs();
        WriteCatalog("xx_XX", Translation("99"));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("No expected LanguageID is configured for non-stub language 'xx_XX'", output);
    }

    [Fact]
    public void Catalog_without_language_id_fails_validation()
    {
        WriteShippedCatalogs();
        WriteCatalog("pt_BR", Translation("2", catalog => catalog.Remove("LanguageID")));

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("pt_BR.json is missing the LanguageID key", output);
    }

    [Fact]
    public void Invalid_json_fails_validation()
    {
        WriteShippedCatalogs();
        WriteRaw("pt_BR", "{ \"LanguageID\": \"2\", ");

        var (exitCode, output) = RunValidatorOnFixtures();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Invalid JSON in", output);
    }
}
