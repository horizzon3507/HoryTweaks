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

# Every catalog listed here is advertised as complete and must mirror en_US key for key.
# A catalog may ship as a LanguageID-only stub only while it is listed in $allowedStubs;
# the runtime falls back to en_US for any key a catalog does not define.
$expectedLanguageIds = @{
    'en_US' = '0'
    'es_419' = '1'
    'pt_BR' = '2'
    'pt_PT' = '3'
    'ko_KR' = '4'
    'ru_RU' = '5'
    'nl_NL' = '6'
    'fil_PH' = '7'
    'fr_FR' = '8'
    'de_DE' = '9'
    'it_IT' = '10'
    'ja_JP' = '11'
    'es_ES' = '12'
    'zh_CN' = '13'
    'zh_TW' = '14'
    'ga_IE' = '15'
}
$allowedStubs = @()

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

function Get-RichTextTags([string]$Value) {
    return [regex]::Matches($Value, '</?[A-Za-z#][^<>]*>') |
        ForEach-Object { $_.Value } |
        Sort-Object
}

function Get-CommandTokens([string]$Value) {
    return [regex]::Matches($Value, '(?<![\w/])/[A-Za-z][\w-]*') |
        ForEach-Object { $_.Value } |
        Sort-Object
}

function Assert-TokenParity([string]$Kind, [string]$Key, [string]$LanguageName, [string[]]$Expected, [string[]]$Actual) {
    if (($Expected -join '|') -cne ($Actual -join '|')) {
        throw "$Kind mismatch for '$Key' in $LanguageName. en_US=[$($Expected -join ', ')], $LanguageName=[$($Actual -join ', ')]"
    }
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
$languageNames = @($languageFiles | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) })

$missingCatalogs = @($expectedLanguageIds.Keys | Sort-Object -CaseSensitive | Where-Object { $_ -cnotin $languageNames })
if ($missingCatalogs.Count -gt 0) {
    throw "Expected language catalogs are missing from $languageDirectory`: [$($missingCatalogs -join ', ')]"
}

foreach ($file in $languageFiles) {
    $language = Read-TranslationFile $file.FullName
    if (-not ($language.Keys -ccontains 'LanguageID')) {
        throw "$($file.Name) is missing the LanguageID key."
    }

    $languageName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    if ($language.Count -eq 1) {
        if ($languageName -cin $allowedStubs) {
            $skippedStubs++
            continue
        }

        throw "$languageName is advertised as a complete catalog but only contains LanguageID. Restore its translations or list it in `$allowedStubs."
    }

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
        $englishValue = [string]$english[$key]
        $languageValue = [string]$language[$key]
        if ([string]::IsNullOrWhiteSpace($languageValue)) {
            throw "Empty translation for '$key' in $languageName."
        }

        Assert-TokenParity 'Placeholder' $key $languageName @(Get-Placeholders $englishValue) @(Get-Placeholders $languageValue)
        Assert-TokenParity 'Rich-text tag' $key $languageName @(Get-RichTextTags $englishValue) @(Get-RichTextTags $languageValue)
        Assert-TokenParity 'Command token' $key $languageName @(Get-CommandTokens $englishValue) @(Get-CommandTokens $languageValue)
    }

    $validatedLanguages++
}

Write-Host "Validated $validatedLanguages language catalogs with $($englishKeys.Count - 1) translation keys, placeholders, rich-text tags and command tokens; skipped $skippedStubs allowed stub catalogs."
