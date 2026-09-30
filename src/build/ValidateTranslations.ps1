param(
    [string]$LanguageDirectory
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($LanguageDirectory)) {
    $sourceDirectory = Split-Path -Parent $PSScriptRoot
    $LanguageDirectory = Join-Path (Join-Path $sourceDirectory 'Resources') 'Lang'
}
$languageDirectory = $LanguageDirectory
$englishPath = Join-Path $languageDirectory 'en_US.json'

$expectedLanguageIds = @{
    'en_US' = '0'
    'es_419' = '1'
    'pt_BR' = '2'
    'pt_PT' = '3'
    'de_DE' = '9'
    'es_ES' = '12'
}

function Read-TranslationFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Translation file not found: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw -Encoding utf8
    try {
        return ConvertFrom-Json -InputObject $content -AsHashtable
    }
    catch {
        throw "Invalid JSON in $Path`: $($_.Exception.Message)"
    }
}

function Get-Placeholders([string]$Value) {
    return [regex]::Matches($Value, '\{\d+\}') |
        ForEach-Object { $_.Value } |
        Sort-Object
}

$english = Read-TranslationFile $englishPath
if (-not ($english.Keys -ccontains 'LanguageID')) {
    throw 'en_US is missing the LanguageID key.'
}

$englishKeys = @($english.Keys | Sort-Object -CaseSensitive)
if ($englishKeys.Count -le 1) {
    throw 'en_US must contain translations in addition to LanguageID.'
}

$validatedLanguages = 0
$skippedStubs = 0
$languageFiles = @(Get-ChildItem -LiteralPath $languageDirectory -Filter '*.json' -File | Sort-Object Name)

foreach ($file in $languageFiles) {
    $language = Read-TranslationFile $file.FullName
    if (-not ($language.Keys -ccontains 'LanguageID')) {
        throw "$($file.Name) is missing the LanguageID key."
    }

    if ($language.Count -eq 1) {
        $skippedStubs++
        continue
    }

    $languageName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    if (-not $expectedLanguageIds.ContainsKey($languageName)) {
        throw "No expected LanguageID is configured for non-stub language '$languageName'."
    }
    if ([string]$language['LanguageID'] -cne $expectedLanguageIds[$languageName]) {
        throw "$languageName LanguageID must be the string '$($expectedLanguageIds[$languageName])'."
    }

    $languageKeys = @($language.Keys | Sort-Object -CaseSensitive)
    $missing = @($englishKeys | Where-Object { $_ -cnotin $languageKeys })
    $extra = @($languageKeys | Where-Object { $_ -cnotin $englishKeys })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        throw "$languageName translation keys differ. Missing: [$($missing -join ', ')]; extra: [$($extra -join ', ')]"
    }

    foreach ($key in $englishKeys) {
        $englishPlaceholders = @(Get-Placeholders ([string]$english[$key]))
        $languagePlaceholders = @(Get-Placeholders ([string]$language[$key]))
        if (($englishPlaceholders -join '|') -cne ($languagePlaceholders -join '|')) {
            throw "Placeholder mismatch for '$key' in $languageName. en_US=[$($englishPlaceholders -join ', ')], $languageName=[$($languagePlaceholders -join ', ')]"
        }
    }

    $validatedLanguages++
}

Write-Host "Validated $validatedLanguages language catalogs with $($englishKeys.Count - 1) translation keys and placeholders; skipped $skippedStubs stub catalogs."
