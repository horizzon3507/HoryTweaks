$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'ValidateTranslations.ps1'
$sourceDirectory = Split-Path -Parent $PSScriptRoot
$languageDirectory = Join-Path (Join-Path $sourceDirectory 'Resources') 'Lang'

$failures = [System.Collections.Generic.List[string]]::new()
$passed = 0

function New-CatalogCopy {
    $copy = Join-Path ([System.IO.Path]::GetTempPath()) ("HoryTweaksLang-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $copy | Out-Null
    Copy-Item -Path (Join-Path $languageDirectory '*.json') -Destination $copy
    return $copy
}

function Read-Catalog([string]$Directory, [string]$Name) {
    $path = Join-Path $Directory "$Name.json"
    return ConvertFrom-Json -InputObject (Get-Content -LiteralPath $path -Raw -Encoding utf8) -AsHashtable
}

function Write-Catalog([string]$Directory, [string]$Name, [hashtable]$Catalog) {
    $path = Join-Path $Directory "$Name.json"
    [System.IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $Catalog -Depth 2), [System.Text.UTF8Encoding]::new($false))
}

function Invoke-Validator([string]$Directory) {
    & $validator -LanguageDirectory $Directory 6>$null | Out-Null
}

function Assert-ValidatorPasses([string]$Name, [scriptblock]$Arrange) {
    $copy = New-CatalogCopy
    try {
        & $Arrange $copy
        Invoke-Validator $copy
        $script:passed++
        Write-Host "PASS $Name"
    }
    catch {
        $script:failures.Add("$Name`: expected the validator to pass but it threw: $($_.Exception.Message)")
        Write-Host "FAIL $Name"
    }
    finally {
        Remove-Item -LiteralPath $copy -Recurse -Force
    }
}

function Assert-ValidatorFails([string]$Name, [string]$ExpectedMessageFragment, [scriptblock]$Arrange) {
    $copy = New-CatalogCopy
    try {
        & $Arrange $copy
        $threw = $false
        try {
            Invoke-Validator $copy
        }
        catch {
            $threw = $true
            $message = $_.Exception.Message
            if ($message -notlike "*$ExpectedMessageFragment*") {
                $script:failures.Add("$Name`: expected an error containing '$ExpectedMessageFragment' but got: $message")
                Write-Host "FAIL $Name"
                return
            }
        }

        if (-not $threw) {
            $script:failures.Add("$Name`: expected the validator to fail but it passed.")
            Write-Host "FAIL $Name"
            return
        }

        $script:passed++
        Write-Host "PASS $Name"
    }
    finally {
        Remove-Item -LiteralPath $copy -Recurse -Force
    }
}

Assert-ValidatorPasses 'accepts the shipped catalogs' { param($dir) }

Assert-ValidatorFails 'rejects a complete catalog that regressed to a stub' 'advertised as a complete catalog' {
    param($dir)
    Write-Catalog $dir 'fr_FR' @{ LanguageID = '8' }
}

Assert-ValidatorFails 'rejects a deleted expected catalog' 'Expected language catalogs are missing' {
    param($dir)
    Remove-Item -LiteralPath (Join-Path $dir 'ja_JP.json')
}

Assert-ValidatorFails 'rejects a catalog missing a key' 'translation keys differ' {
    param($dir)
    $catalog = Read-Catalog $dir 'ko_KR'
    $catalog.Remove('Command.Help.Body')
    Write-Catalog $dir 'ko_KR' $catalog
}

Assert-ValidatorFails 'rejects an unexpected LanguageID' 'LanguageID must be the string' {
    param($dir)
    $catalog = Read-Catalog $dir 'zh_TW'
    $catalog['LanguageID'] = '13'
    Write-Catalog $dir 'zh_TW' $catalog
}

Assert-ValidatorFails 'rejects an empty translation' 'Empty translation' {
    param($dir)
    $catalog = Read-Catalog $dir 'it_IT'
    $catalog['Host'] = ''
    Write-Catalog $dir 'it_IT' $catalog
}

Assert-ValidatorFails 'rejects a dropped placeholder' 'Placeholder mismatch' {
    param($dir)
    $catalog = Read-Catalog $dir 'nl_NL'
    $catalog['HostTools.KickMessage'] = 'Reden: <color=#fc0000>{1}</color>'
    Write-Catalog $dir 'nl_NL' $catalog
}

Assert-ValidatorFails 'rejects a broken rich-text tag' 'Rich-text tag mismatch' {
    param($dir)
    $catalog = Read-Catalog $dir 'ru_RU'
    $catalog['BetterOption.SendBetterRpc'] = '<color=#4f92ff>RPC</colour>'
    Write-Catalog $dir 'ru_RU' $catalog
}

Assert-ValidatorFails 'rejects a translated command token' 'Command token mismatch' {
    param($dir)
    $catalog = Read-Catalog $dir 'fil_PH'
    $catalog['Command.Help.Body'] = $catalog['Command.Help.Body'].Replace('/commands', '/mgautos')
    Write-Catalog $dir 'fil_PH' $catalog
}

Write-Host "$passed passed, $($failures.Count) failed."
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ }
    exit 1
}
