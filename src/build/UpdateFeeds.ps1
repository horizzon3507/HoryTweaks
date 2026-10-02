#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Regenerates the machine-consumed feeds under api/ for a published release tag.
.DESCRIPTION
    Rewrites three files so the in-mod updater and news feed point at a release:

      - api/update.json      legacy feed (dllLink + release metadata fields)
      - api/update-V2.json   current feed read by src/Network/Loaders/BAUUpdateLoader.cs
      - api/manifest.json    News index rebuilt from the files under api/news/

    .github/workflows/update-feeds.yml runs this on every published release; run
    it by hand to repair the feeds:

        pwsh src/build/UpdateFeeds.ps1 -Tag v0.1.3 [-DllUrl <url>] [-RequireNews]
.PARAMETER Tag
    Release tag, with or without the leading v (v0.1.3 or 0.1.3). Optional
    channel suffixes map onto the legacy feed fields per VERSIONING.md and
    src/Enums/ReleaseTypes.cs: -stable/none => releaseType 0, -beta[N] => 1,
    anything else (alpha, rc, ...) => 2; -HN sets isHotfix/hotfixNumber.
.PARAMETER DllUrl
    browser_download_url of the release's HoryTweaks.dll asset. Defaults to the
    canonical https://github.com/<RepoSlug>/releases/download/<tag>/HoryTweaks.dll
    that tests/HoryTweaks.Tests/UpdateManifestTests.cs asserts.
.PARAMETER SteamEpicMsStorePackageUrl
    browser_download_url of the release's full Steam/Epic/Microsoft Store
    package (HoryTweaks-Steam-Epic-MsStore-<tag>.zip). Defaults to the canonical
    asset URL; consumed by the in-game updater's store-package path.
.PARAMETER ItchioPackageUrl
    browser_download_url of the release's itch.io package
    (HoryTweaks-Itchio-<tag>.zip). Defaults to the canonical asset URL.
.PARAMETER Sha256SumsUrl
    browser_download_url of the release's SHA256SUMS.txt checksum manifest.
    Defaults to the canonical asset URL.
.PARAMETER RepoSlug
    owner/name used for the canonical download URL.
.PARAMETER RepoRoot
    Repository root containing api/. Defaults to the repo root relative to this script.
.PARAMETER RequireNews
    Fail when api/news/ has no yaml file mentioning the tagged version.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [string]$DllUrl,

    [string]$SteamEpicMsStorePackageUrl,

    [string]$ItchioPackageUrl,

    [string]$Sha256SumsUrl,

    [string]$RepoSlug = 'horizzon3507/HoryTweaks',

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,

    [switch]$RequireNews
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Tag -notmatch '^v?(?<core>\d+\.\d+\.\d+)(?<suffix>[-.][A-Za-z0-9.\-_]*)?$') {
    throw "Tag must look like v0.1.3, optionally with a channel/hotfix suffix: $Tag"
}
$version = $Tag.TrimStart('v', 'V')
$tagName = "v$version"

$releaseType = 0
$isHotfix = $false
$betaNumber = 0
$hotfixNumber = 0
$hasOtherSuffix = $false
$suffix = ''
if ($Matches['suffix']) {
    $suffix = $Matches['suffix']
}
$tokens = @($suffix -split '[.\-_]' | Where-Object { $_ })
for ($i = 0; $i -lt $tokens.Count; $i++) {
    $token = $tokens[$i]
    if ($token -match '^(?i)beta(?<n>\d+)?$') {
        $releaseType = 1
        $betaNumber = 1
        if ($Matches['n']) { $betaNumber = [int]$Matches['n'] }
        elseif ($i + 1 -lt $tokens.Count -and $tokens[$i + 1] -match '^\d+$') {
            $betaNumber = [int]$tokens[$i + 1]
        }
    }
    elseif ($token -match '^(?i)h(?<n>\d+)$') {
        $isHotfix = $true
        $hotfixNumber = [int]$Matches['n']
    }
    elseif ($token -notmatch '^(?i)(stable|release|final)$' -and $token -notmatch '^\d+$') {
        $hasOtherSuffix = $true
    }
}
if ($releaseType -eq 0 -and $hasOtherSuffix) {
    $releaseType = 2
}

$releaseBaseUrl = "https://github.com/$RepoSlug/releases/download/$tagName"
if (-not $DllUrl) {
    $DllUrl = "$releaseBaseUrl/HoryTweaks.dll"
}
if (-not $SteamEpicMsStorePackageUrl) {
    $SteamEpicMsStorePackageUrl = "$releaseBaseUrl/HoryTweaks-Steam-Epic-MsStore-$tagName.zip"
}
if (-not $ItchioPackageUrl) {
    $ItchioPackageUrl = "$releaseBaseUrl/HoryTweaks-Itchio-$tagName.zip"
}
if (-not $Sha256SumsUrl) {
    $Sha256SumsUrl = "$releaseBaseUrl/SHA256SUMS.txt"
}

$apiDir = Join-Path $RepoRoot 'api'
$newsDir = Join-Path $apiDir 'news'

if ($RequireNews) {
    $needle = [regex]::Escape("v$version") + '(-H\d+)?\.yaml$'
    $hasEntry = Get-ChildItem -LiteralPath $newsDir -Filter '*.yaml' -File |
        Where-Object { $_.Name -match $needle } |
        Select-Object -First 1
    if (-not $hasEntry) {
        throw "api/news/ has no yaml entry for $tagName"
    }
}

$newsFiles = Get-ChildItem -LiteralPath $newsDir -Filter '*.yaml' -File
$entries = foreach ($file in $newsFiles) {
    $match = [regex]::Match($file.Name, 'v(?<maj>\d+)\.(?<min>\d+)\.(?<pat>\d+)(?:-H(?<hot>\d+))?')
    $isTemplate = $file.Name -eq 'Template.yaml'
    [pscustomobject]@{
        Name      = $file.Name
        IsTemplate = $isTemplate
        Versioned = $match.Success -and -not $isTemplate
        Major     = if ($match.Success) { [int]$match.Groups['maj'].Value } else { 0 }
        Minor     = if ($match.Success) { [int]$match.Groups['min'].Value } else { 0 }
        Patch     = if ($match.Success) { [int]$match.Groups['pat'].Value } else { 0 }
        Hotfix    = if ($match.Success -and $match.Groups['hot'].Success) { [int]$match.Groups['hot'].Value } else { 0 }
    }
}
$template = @($entries | Where-Object IsTemplate | Sort-Object Name)
$rest = @($entries | Where-Object { -not $_.IsTemplate } |
    Sort-Object @{ Expression = { if ($_.Versioned) { 0 } else { 1 } } }, Major, Minor, Patch, Hotfix, Name)
$news = [string[]](@($template + $rest) | ForEach-Object Name)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
function Write-Feed([string]$Name, [System.Collections.IDictionary]$Data) {
    $path = Join-Path $apiDir $Name
    $json = ($Data | ConvertTo-Json -Depth 5) + "`n"
    [System.IO.File]::WriteAllText($path, $json, $utf8NoBom)
    Write-Host "wrote $path"
}

Write-Feed 'update.json' ([ordered]@{
    dllLink       = $DllUrl
    version       = $version
    releaseType   = $releaseType
    isHotfix      = $isHotfix
    betaNumber    = $betaNumber
    hotfixNumber  = $hotfixNumber
})
Write-Feed 'update-V2.json' ([ordered]@{
    valid      = $true
    dllLink    = $DllUrl
    version    = $version
    packages   = [ordered]@{
        steamEpicMsStore = $SteamEpicMsStorePackageUrl
        itchio           = $ItchioPackageUrl
    }
    sha256Link = $Sha256SumsUrl
})
Write-Feed 'manifest.json' ([ordered]@{ News = $news })
