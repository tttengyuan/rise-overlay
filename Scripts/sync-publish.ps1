# Sync a runnable publish folder for Rise Overlay (after managed build).
# Usage: powershell -ExecutionPolicy Bypass -File Scripts\sync-publish.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rel = Join-Path $root 'HunterPie\bin\Release\net10.0-windows7.0'
$pub = Join-Path $root 'publish\RiseOverlay'

if (-not (Test-Path (Join-Path $rel 'HunterPie.exe'))) {
    Write-Host 'Building Release...'
    Push-Location $root
    dotnet build HunterPie\HunterPie.csproj -c Release --verbosity quiet
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $pub | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $pub 'libs') | Out-Null

# Languages: post-build copies from Localization submodule; ensure present
$langSrc = Join-Path $root 'Localization\localization'
$langDst = Join-Path $pub 'Languages'
New-Item -ItemType Directory -Force -Path $langDst | Out-Null
if (Test-Path $langSrc) {
    Copy-Item -Force (Join-Path $langSrc '*.xml') $langDst
} elseif (Test-Path (Join-Path $rel 'Languages')) {
    Copy-Item -Recurse -Force (Join-Path $rel 'Languages\*') $langDst
}

# Core runtime files
Get-ChildItem $rel -File | Where-Object {
    $_.Extension -in '.exe','.dll','.json' -and $_.Name -notmatch '\.pdb$'
} | Copy-Item -Force -Destination $pub

foreach ($dir in @('Address','Assets','Themes','Game','static')) {
    $s = Join-Path $rel $dir
    if (Test-Path $s) {
        Copy-Item -Recurse -Force $s (Join-Path $pub $dir)
    }
}

$nativeCandidates = @(
    (Join-Path $rel 'libs\HunterPie.Native.dll'),
    (Join-Path $root 'HunterPie\bin\Debug\net10.0-windows7.0\libs\HunterPie.Native.dll'),
    (Join-Path $pub 'libs\HunterPie.Native.dll')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($nativeCandidates) {
    try {
        Copy-Item -Force $nativeCandidates (Join-Path $pub 'libs\HunterPie.Native.dll')
    } catch {
        Write-Host "Native DLL locked (app running?) — skipped copy: $_"
    }
}

# Self-update helper (must ship next to HunterPie.exe)
$updaterSrc = Join-Path $PSScriptRoot 'rise-overlay-updater.ps1'
if (Test-Path $updaterSrc) {
    Copy-Item -Force $updaterSrc (Join-Path $pub 'rise-overlay-updater.ps1')
}

Write-Host "Ready: $(Join-Path $pub 'HunterPie.exe')"
Write-Host "Languages en-us: $(Test-Path (Join-Path $langDst 'en-us.xml'))"
Write-Host "Native: $(Test-Path (Join-Path $pub 'libs\HunterPie.Native.dll'))"
Write-Host "Updater: $(Test-Path (Join-Path $pub 'rise-overlay-updater.ps1'))"
