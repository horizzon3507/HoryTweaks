$ErrorActionPreference = 'Stop'

$sourceDirectory = Split-Path -Parent $PSScriptRoot
$languageDirectory = Join-Path (Join-Path $sourceDirectory 'Resources') 'Lang'
$englishPath = Join-Path $languageDirectory 'en_US.json'
$portuguesePath = Join-Path $languageDirectory 'pt_BR.json'

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
$portuguese = Read-TranslationFile $portuguesePath

if ($portuguese['LanguageID'] -ne '2') {
    throw "pt_BR LanguageID must be the string '2'."
}

$englishKeys = @($english.Keys | Sort-Object -CaseSensitive)
$portugueseKeys = @($portuguese.Keys | Sort-Object -CaseSensitive)
$missing = @($englishKeys | Where-Object { $_ -cnotin $portugueseKeys })
$extra = @($portugueseKeys | Where-Object { $_ -cnotin $englishKeys })
if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
    throw "Translation keys differ. Missing: [$($missing -join ', ')]; extra: [$($extra -join ', ')]"
}

foreach ($key in $englishKeys) {
    $englishPlaceholders = @(Get-Placeholders ([string]$english[$key]))
    $portuguesePlaceholders = @(Get-Placeholders ([string]$portuguese[$key]))
    if (($englishPlaceholders -join '|') -cne ($portuguesePlaceholders -join '|')) {
        throw "Placeholder mismatch for '$key'. en_US=[$($englishPlaceholders -join ', ')], pt_BR=[$($portuguesePlaceholders -join ', ')]"
    }
}

Write-Host "Validated $($englishKeys.Count - 1) translation keys and placeholders."
