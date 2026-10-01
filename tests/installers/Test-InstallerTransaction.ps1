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
}
finally {
    ${function:Copy-AtomicFile} = $originalCopy
    Remove-Item -LiteralPath $root -Recurse -Force
}
