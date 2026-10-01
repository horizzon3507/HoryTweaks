$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../../installers/Install-HoryTweaks.ps1"
$root = Join-Path ([IO.Path]::GetTempPath()) ('hory-transaction-' + [guid]::NewGuid().ToString('N'))
$game = Join-Path $root 'Game'
$stage = Join-Path $root 'Stage'
$files = @('BepInEx/core/new.dll', 'BepInEx/plugins/HoryTweaks.dll', 'winhttp.dll')
$originalCopy = ${function:Copy-AtomicFile}
try {
    foreach ($dir in @($game, $stage)) {
        [IO.Directory]::CreateDirectory((Join-Path $dir 'BepInEx/plugins')) | Out-Null
        [IO.Directory]::CreateDirectory((Join-Path $dir 'BepInEx/core')) | Out-Null
    }
    foreach ($name in $files) { [IO.File]::WriteAllText((Join-Path $stage $name), 'new') }
    foreach ($name in @('BepInEx/plugins/HoryTweaks.dll', 'winhttp.dll', $Legacy)) {
        [IO.File]::WriteAllText((Join-Path $game $name), 'original')
    }
    $script:failedOnce = $false
    function Copy-AtomicFile([string]$Source, [string]$Target) {
        if (-not $script:failedOnce -and (Split-Path -Leaf $Target) -eq 'winhttp.dll') {
            $script:failedOnce = $true
            throw 'Injected write failure'
        }
        & $originalCopy $Source $Target
    }
    $caught = $false
    try { Install-ModFiles $game $stage $files 'test' 'test' }
    catch {
        if ($_.Exception.Message -notlike '*Injected write failure*') { throw }
        $caught = $true
    }
    if (-not $caught) { throw 'Write failure did not occur.' }
    foreach ($name in @('BepInEx/plugins/HoryTweaks.dll', 'winhttp.dll', $Legacy)) {
        if ([IO.File]::ReadAllText((Join-Path $game $name)) -ne 'original') { throw "Rollback did not restore $name" }
    }
    if (Test-Path (Join-Path $game 'BepInEx/core/new.dll')) { throw 'Rollback left a newly installed file.' }
    if (Test-Path (Join-Path $game '.horytweaks-install.lock')) { throw 'Rollback left the lock.' }
    Write-Host 'PASS: partial installation rolled back and backup retained.'

    if ($env:OS -eq 'Windows_NT') {
        $outside = Join-Path $root 'outside'
        [IO.Directory]::CreateDirectory($outside) | Out-Null
        New-Item -ItemType Junction -Path (Join-Path $game 'dotnet') -Target $outside | Out-Null
        $rejected = $false
        try { Assert-TargetPath $game 'dotnet/runtime.dll' }
        catch { $rejected = $true }
        if (-not $rejected) { throw 'Target junction was accepted.' }
        Write-Host 'PASS: destination junction rejected.'
        Remove-Item -LiteralPath (Join-Path $game 'dotnet')
    }

    $package = Join-Path $root 'pkg.zip'
    [IO.File]::WriteAllText($package, 'zip')
    $manifest = Join-Path $root 'SHA256SUMS.txt'
    $hash = 'A' * 64
    [IO.File]::WriteAllText($manifest, "$hash  pkg.zip`n" + ('B' * 64) + "  other.zip`n")
    if ((Get-ManifestHash (Get-Content -LiteralPath $manifest -Raw) 'pkg.zip') -ne $hash) { throw 'Manifest lookup failed.' }
    if ($null -ne (Get-ManifestHash 'no entries' 'pkg.zip')) { throw 'Missing manifest entry should return null.' }
    $duplicates = "$hash  pkg.zip`n$hash  pkg.zip`n"
    $rejected = $false
    try { Get-ManifestHash $duplicates 'pkg.zip' | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Duplicate manifest entries were accepted.' }
    $sidecar = Get-SidecarHash $package
    if ($null -eq $sidecar -or $sidecar.Hash -ne $hash -or $sidecar.Source -ne $manifest) { throw 'Sidecar SHA256SUMS.txt not used.' }
    Remove-Item -LiteralPath $manifest
    $bare = 'C' * 64
    [IO.File]::WriteAllText("$package.sha256", "$bare`n")
    $sidecar = Get-SidecarHash $package
    if ($null -eq $sidecar -or $sidecar.Hash -ne $bare) { throw 'Sidecar .sha256 file not used.' }
    if ($null -ne (Get-SidecarHash (Join-Path $root 'absent.zip'))) { throw 'Absent sidecar should return null.' }
    Write-Host 'PASS: checksum manifest and sidecar lookup.'

    $script:readHostAnswer = 'n'
    function Read-Host { param([object]$Prompt) $script:readHostAnswer }
    if (Confirm-Unverified 'test-subject') { throw 'Declined unverified install should cancel.' }
    $script:readHostAnswer = 'yes'
    if (-not (Confirm-Unverified 'test-subject')) { throw 'Confirmed unverified install should continue.' }
    Write-Host 'PASS: missing manifest requires interactive confirmation.'
}
finally {
    ${function:Copy-AtomicFile} = $originalCopy
    Remove-Item -LiteralPath $root -Recurse -Force
}
