#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$GameDir,
    [ValidateSet('steam', 'epic', 'msstore', 'itch')][string]$Store,
    [string]$Version = 'latest',
    [string]$Package,
    [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$Sha256,
    [switch]$SkipChecksum,
    [switch]$DryRun,
    [switch]$Yes,
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Releases = 'https://github.com/horizzon3507/HoryTweaks/releases'
$Legacy = 'BepInEx/plugins/BetterAmongUs.dll'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Net.Http

function Find-GameDirectories {
    $roots = [System.Collections.Generic.List[string]]::new()
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        if (Test-Path $key) {
            $value = $null
            try { $value = Get-ItemPropertyValue -Path $key -Name 'SteamPath' } catch {}
            if (-not $value) { try { $value = Get-ItemPropertyValue -Path $key -Name 'InstallPath' } catch {} }
            if ($value) { $roots.Add($value) }
        }
    }
    if (${env:ProgramFiles(x86)}) { $roots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam')) }
    $libraries = [System.Collections.Generic.List[string]]::new()
    foreach ($root in $roots) {
        $libraries.Add($root)
        $vdf = Join-Path $root 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf -PathType Leaf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s*"([^"]+)"')) {
                $libraries.Add($match.Groups[1].Value.Replace('\\', '\'))
            }
        }
    }
    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($library in $libraries) { $candidates.Add((Join-Path $library 'steamapps/common/Among Us')) }
    if ($env:ProgramData) {
        $manifests = Join-Path $env:ProgramData 'Epic/EpicGamesLauncher/Data/Manifests'
        if (Test-Path -LiteralPath $manifests) {
            foreach ($file in Get-ChildItem -LiteralPath $manifests -Filter '*.item') {
                try {
                    $manifest = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
                    if ($manifest.DisplayName -eq 'Among Us') { $candidates.Add($manifest.InstallLocation) }
                }
                catch { Write-Verbose "Skipping unreadable Epic manifest: $($file.Name)" }
            }
        }
    }
    $candidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'Among Us.exe') -PathType Leaf } | Sort-Object -Unique
}

function Get-ReleaseFile([string]$Url, [string]$Destination, [switch]$Optional, [switch]$ResolveTag) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $client = [System.Net.Http.HttpClient]::new()
        $client.Timeout = [TimeSpan]::FromSeconds(300)
        $client.MaxResponseContentBufferSize = 256MB
        $client.DefaultRequestHeaders.UserAgent.ParseAdd('HoryTweaks-Installer/1.0')
        $response = $null
        try {
            $response = $client.GetAsync($Url).GetAwaiter().GetResult()
            if ($Optional -and [int]$response.StatusCode -eq 404) { return $false }
            $response.EnsureSuccessStatusCode() | Out-Null
            $resolved = $response.RequestMessage.RequestUri
            if ($resolved.Scheme -ne 'https') { throw 'Download redirected outside HTTPS.' }
            if ($ResolveTag) { return $resolved.AbsoluteUri }
            $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            [IO.File]::WriteAllBytes($Destination, $bytes)
            return $true
        }
        catch {
            if ($attempt -eq 3) { throw "Download failed: $Url. $($_.Exception.Message)" }
            Start-Sleep -Seconds 2
        }
        finally {
            if ($null -ne $response) { $response.Dispose() }
            $client.Dispose()
        }
    }
}

function Resolve-ReleaseVersion([string]$Tag) {
    if ($Tag -eq 'latest') {
        $url = Get-ReleaseFile -Url "$Releases/latest" -ResolveTag
        if (-not $url.StartsWith("$Releases/tag/", [StringComparison]::Ordinal)) { throw 'Cannot resolve latest release. Use -Version v0.1.2.' }
        $Tag = $url.Substring("$Releases/tag/".Length)
    }
    if (-not $Tag.StartsWith('v')) { $Tag = "v$Tag" }
    if ($Tag -cnotmatch '^v[0-9]+\.[0-9]+\.[0-9]+([.-][A-Za-z0-9.-]+)?$') { throw 'Invalid release tag.' }
    return $Tag
}

function Get-Sha256([string]$Path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try {
        $bytes = $algorithm.ComputeHash($stream)
        return ([BitConverter]::ToString($bytes)).Replace('-', '')
    }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Get-ManifestHash([string]$Text, [string]$Asset) {
    $found = [regex]::Matches($Text, '(?m)^([0-9a-fA-F]{64}) [ *]' + [regex]::Escape($Asset) + '\r?$')
    if ($found.Count -gt 1) { throw 'Checksum manifest lists this package more than once.' }
    if ($found.Count -eq 0) { return $null }
    return $found[0].Groups[1].Value
}

function Get-SidecarHash([string]$PackagePath) {
    $name = Split-Path -Leaf $PackagePath
    $candidates = @((Join-Path (Split-Path -Parent $PackagePath) 'SHA256SUMS.txt'), "$PackagePath.sha256")
    if ([IO.Path]::GetExtension($PackagePath)) { $candidates += [IO.Path]::ChangeExtension($PackagePath, '.sha256') }
    foreach ($file in $candidates) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        $text = Get-Content -LiteralPath $file -Raw
        $hash = Get-ManifestHash $text $name
        if ($hash) { return @{ Hash = $hash; Source = $file } }
        $bare = $text.Trim()
        if ($bare -match '^[0-9a-fA-F]{64}$') { return @{ Hash = $bare; Source = $file } }
    }
    return $null
}

function Get-ReleaseManifestHash([string]$Work, [string]$Version, [string]$Asset) {
    try { $tag = Resolve-ReleaseVersion $Version } catch { return $null }
    $checksums = Join-Path $Work 'SHA256SUMS.txt'
    try {
        if (-not (Get-ReleaseFile -Url "$Releases/download/$tag/SHA256SUMS.txt" -Destination $checksums -Optional)) { return $null }
    }
    catch { return $null }
    return Get-ManifestHash (Get-Content -LiteralPath $checksums -Raw) $Asset
}

function Confirm-Unverified([string]$Subject) {
    Write-Host "WARNING: $Subject has no checksum manifest; the package cannot be verified." -ForegroundColor Yellow
    if ($Yes) { return $true }
    try { $answer = Read-Host 'Continue without checksum verification? [y/N]' }
    catch { return $true }
    return $answer -in @('y', 'yes')
}

function Assert-SafeArchiveName([string]$Name) {
    $parts = $Name.TrimEnd('/').Split('/')
    foreach ($part in $parts) {
        if (-not $part -or $part -in @('.', '..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or
            $part -match '[\\\x00-\x1f<>:"|?*]' -or $part -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$') {
            throw "Unsafe archive path: $Name"
        }
    }
}

function Assert-TargetPath([string]$Game, [string]$Relative) {
    $current = $Game
    $parts = $Relative.Split('/')
    for ($i = 0; $i -lt $parts.Count; $i++) {
        $current = Join-Path $current $parts[$i]
        $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        if ($null -ne $item) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing link or junction: $current" }
            if (($i -eq $parts.Count - 1 -and $item.PSIsContainer) -or ($i -lt $parts.Count - 1 -and -not $item.PSIsContainer)) {
                throw "Unexpected file/directory: $current"
            }
        }
    }
}

function Expand-ModPackage([string]$Archive, [string]$Stage) {
    $files = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$total = 0
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        if ($zip.Entries.Count -gt 10000) { throw 'Archive has too many entries.' }
        foreach ($entry in $zip.Entries) {
            Assert-SafeArchiveName $entry.FullName
            $name = $entry.FullName.TrimEnd('/')
            $mode = ($entry.ExternalAttributes -shr 16) -band 61440
            if ($mode -notin @(0, 32768, 16384) -or -not $seen.Add($name)) { throw "Unsupported or duplicate archive entry: $name" }
            $total += $entry.Length
            if ($total -gt 1GB) { throw 'Archive is too large.' }
            if ($entry.FullName.EndsWith('/')) { continue }
            if ($name -match '^(Better_Data/|BepInEx/config/)') { continue }
            if ($name -notmatch '^(BepInEx/core/|BepInEx/patchers/|dotnet/)' -and
                $name -cnotin @('BepInEx/plugins/HoryTweaks.dll', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt')) {
                throw "Unexpected file in mod package: $name"
            }
            $target = Join-Path $Stage $name
            [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target)
            $files.Add($name)
        }
    }
    finally { $zip.Dispose() }
    foreach ($required in @('BepInEx/plugins/HoryTweaks.dll', 'winhttp.dll', 'doorstop_config.ini')) {
        if (-not $files.Contains($required)) { throw 'Not a full HoryTweaks package; runtime or plugin is missing.' }
    }
    if (@($files | Where-Object { $_.StartsWith('BepInEx/core/') }).Count -eq 0) { throw 'Package is missing BepInEx/core.' }
    foreach ($name in @('BepInEx/plugins/HoryTweaks.dll', 'winhttp.dll')) {
        $stream = [IO.File]::OpenRead((Join-Path $Stage $name))
        try {
            if ($stream.ReadByte() -ne 77 -or $stream.ReadByte() -ne 90) { throw "Invalid Windows DLL: $name" }
        }
        finally { $stream.Dispose() }
    }
    $files | Sort-Object
}

function Copy-AtomicFile([string]$Source, [string]$Target) {
    $parent = Split-Path -Parent $Target
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $temporary = Join-Path $parent ('.horytweaks-' + [guid]::NewGuid().ToString('N'))
    try {
        [IO.File]::Copy($Source, $temporary)
        if ([IO.File]::Exists($Target)) { [IO.File]::Replace($temporary, $Target, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $Target) }
    }
    finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function Install-ModFiles([string]$Game, [string]$Stage, [string[]]$Files, [string]$Tag, [string]$Digest) {
    $lock = Join-Path $Game '.horytweaks-install.lock'
    New-Item -ItemType Directory -Path $lock -ErrorAction Stop | Out-Null
    $backup = $null
    $touched = [System.Collections.Generic.List[string]]::new()
    $originals = [ordered]@{}
    try {
        foreach ($name in @($Files) + @($Legacy)) { Assert-TargetPath $Game $name }
        Assert-TargetPath $Game '.horytweaks-backups/probe'
        $backup = Join-Path $Game ('.horytweaks-backups/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ-') + [guid]::NewGuid().ToString('N').Substring(0, 8))
        [IO.Directory]::CreateDirectory($backup) | Out-Null
        foreach ($name in @($Files) + @($Legacy)) {
            $source = Join-Path $Game $name
            $originals[$name] = [IO.File]::Exists($source)
            if ($originals[$name]) {
                $target = Join-Path $backup "files/$name"
                [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
                [IO.File]::Copy($source, $target)
            }
        }
        @{ version = $Tag; sha256 = $Digest; game = $Game; originals = $originals } |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'manifest.json') -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $backup 'status.txt') -Value 'installing'
        foreach ($name in $Files) {
            $touched.Add($name)
            Copy-AtomicFile (Join-Path $Stage $name) (Join-Path $Game $name)
        }
        if ($originals[$Legacy]) {
            $touched.Add($Legacy)
            [IO.File]::Delete((Join-Path $Game $Legacy))
        }
        Set-Content -LiteralPath (Join-Path $backup 'status.txt') -Value 'installed'
        Write-Host "Installed $Tag. Backup and file manifest: $backup"
    }
    catch {
        $failure = $_
        $rollbackFailed = $false
        for ($i = $touched.Count - 1; $i -ge 0; $i--) {
            $name = $touched[$i]
            try {
                if ($originals[$name]) { Copy-AtomicFile (Join-Path $backup "files/$name") (Join-Path $Game $name) }
                else { [IO.File]::Delete((Join-Path $Game $name)) }
            }
            catch {
                $rollbackFailed = $true
                Write-Warning "Manual recovery required for ${name}: $($_.Exception.Message)"
            }
        }
        if ($backup) {
            $status = if ($rollbackFailed) { 'rollback-failed' } else { 'rolled-back' }
            Set-Content -LiteralPath (Join-Path $backup 'status.txt') -Value $status
            Write-Warning "Installation failed. Backup: $backup"
        }
        throw $failure
    }
    finally { Remove-Item -LiteralPath $lock -Force }
}

function Invoke-ModInstaller {
    if ($Help) {
        Write-Host @'
HoryTweaks Windows installer (close Among Us first).
install-horytweaks.bat [-GameDir "C:\Games\Among Us"] [-Store steam|epic|msstore|itch]
  [-Version latest|v0.1.2] [-DryRun] [-Yes] [-SkipChecksum]
  [-Package "C:\Downloads\package.zip" [-Sha256 <trusted ZIP hash>]]
A sidecar SHA256SUMS.txt or <package>.sha256 beside the ZIP is also accepted.
Steam libraries and Epic installations are detected. Other stores accept a manual path.
Use -DryRun to validate without changing game files. Keep Install-HoryTweaks.ps1 beside the BAT.
'@
        return
    }
    if (-not $GameDir) {
        $games = @(Find-GameDirectories)
        for ($i = 0; $i -lt $games.Count; $i++) { Write-Host "$($i + 1). $($games[$i])" }
        $answer = Read-Host 'Select a game number or enter its full folder path'
        $choice = 0
        if ([int]::TryParse($answer, [ref]$choice) -and $choice -ge 1 -and $choice -le $games.Count) { $GameDir = $games[$choice - 1] }
        else { $GameDir = $answer.Trim('"') }
    }
    $game = (Get-Item -LiteralPath $GameDir -Force).FullName
    if (-not (Test-Path -LiteralPath (Join-Path $game 'Among Us.exe') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $game 'Among Us_Data') -PathType Container)) {
        throw 'Select the game folder containing Among Us.exe and Among Us_Data.'
    }
    if (Get-Process -Name 'Among Us' -ErrorAction SilentlyContinue) { throw 'Close Among Us before installing.' }
    if (-not $Store) {
        $Store = (Read-Host 'Store [steam/epic/msstore/itch]').ToLowerInvariant()
        if ($Store -notin @('steam', 'epic', 'msstore', 'itch')) { throw 'Unknown store.' }
    }
    $work = Join-Path ([IO.Path]::GetTempPath()) ('horytweaks-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($work) | Out-Null
    try {
        $expected = $Sha256
        if ($Package) {
            $archive = (Get-Item -LiteralPath $Package).FullName
            $tag = if ($Version -eq 'latest') { 'offline' } else { Resolve-ReleaseVersion $Version }
            if (-not $expected -and -not $SkipChecksum) {
                $sidecar = Get-SidecarHash $archive
                if ($null -ne $sidecar) {
                    $expected = $sidecar.Hash
                    Write-Host "Verifying against $($sidecar.Source)."
                }
                else {
                    $expected = Get-ReleaseManifestHash $work $Version (Split-Path -Leaf $archive)
                    if ($expected) { Write-Host 'Verifying against the release checksum manifest.' }
                }
                if (-not $expected) {
                    throw '-Package requires -Sha256, a sidecar SHA256SUMS.txt or <package>.sha256, or -SkipChecksum to install unverified.'
                }
            }
        }
        else {
            $tag = Resolve-ReleaseVersion $Version
            $platform = if ($Store -eq 'itch') { 'Itchio' } else { 'Steam-Epic-MsStore' }
            $asset = "HoryTweaks-$platform-$tag.zip"
            $base = "$Releases/download/$tag/"
            $archive = Join-Path $work $asset
            Write-Host "Downloading $asset..."
            Get-ReleaseFile -Url ($base + $asset) -Destination $archive | Out-Null
            if (-not $expected -and -not $SkipChecksum) {
                $checksums = Join-Path $work 'SHA256SUMS.txt'
                if (Get-ReleaseFile -Url ($base + 'SHA256SUMS.txt') -Destination $checksums -Optional) {
                    $expected = Get-ManifestHash (Get-Content -LiteralPath $checksums -Raw) $asset
                    if (-not $expected) { throw 'Release checksum manifest does not identify this package.' }
                }
                elseif (-not (Confirm-Unverified "release $tag")) { Write-Host 'Cancelled. No game files changed.'; return }
            }
        }
        if ($SkipChecksum -and -not $expected) { Write-Host 'WARNING: checksum verification skipped; installing an unverified package.' -ForegroundColor Yellow }
        $digest = Get-Sha256 $archive
        if ($expected -and $digest -ne $expected) { throw 'SHA-256 mismatch. Nothing was installed.' }
        Write-Host "ZIP SHA-256: $digest"
        $stage = Join-Path $work 'stage'
        $files = @(Expand-ModPackage $archive $stage)
        [long]$needed = 64MB
        foreach ($name in @($files) + @($Legacy)) {
            Assert-TargetPath $game $name
            $old = Join-Path $game $name
            if ([IO.File]::Exists($old)) { $needed += (Get-Item -LiteralPath $old -Force).Length }
        }
        foreach ($name in $files) { $needed += (Get-Item -LiteralPath (Join-Path $stage $name) -Force).Length }
        $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($game))
        if ($drive.IsReady -and $drive.AvailableFreeSpace -lt $needed) { throw 'Not enough free space for installation and backup.' }
        Write-Host "Target: $game`nStore: $Store`nRelease: $tag`nFiles: $($files.Count)"
        Write-Host 'Better_Data, BepInEx/config and other plugins are preserved. BetterAmongUs.dll is backed up and removed.'
        if ($DryRun) { Write-Host 'Dry run complete. No game files changed.'; return }
        if (-not $Yes -and (Read-Host 'Install? [y/N]') -notin @('y', 'yes')) { Write-Host 'Cancelled. No game files changed.'; return }
        if (Get-Process -Name 'Among Us' -ErrorAction SilentlyContinue) { throw 'Close Among Us before installing.' }
        Install-ModFiles $game $stage $files $tag $digest
        Write-Host 'Done. Start Among Us from your game store.'
    }
    finally { Remove-Item -LiteralPath $work -Recurse -Force }
}

if ($MyInvocation.InvocationName -ne '.') {
    try { Invoke-ModInstaller }
    catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
