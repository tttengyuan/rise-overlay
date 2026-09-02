# Rise Overlay file replacer — launched by the app after downloading a Release zip.
param(
    [Parameter(Mandatory = $true)][string]$TargetDir,
    [Parameter(Mandatory = $true)][string]$SourceDir,
    [Parameter(Mandatory = $true)][string]$RestartExe,
    [int]$WaitPid = 0
)

$ErrorActionPreference = 'Stop'

if ($WaitPid -gt 0) {
    try { Wait-Process -Id $WaitPid -Timeout 120 -ErrorAction SilentlyContinue } catch { }
    Start-Sleep -Seconds 1
}

if (-not (Test-Path (Join-Path $SourceDir 'HunterPie.exe'))) {
    Write-Error "Source package missing HunterPie.exe: $SourceDir"
    exit 1
}

$skipNames = @('HunterPie_Log.txt', 'config.json', 'config.json.bak')

Get-ChildItem -LiteralPath $SourceDir -Force | ForEach-Object {
    if ($skipNames -contains $_.Name) { return }
    $dest = Join-Path $TargetDir $_.Name
    if ($_.PSIsContainer) {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
    } else {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
    }
}

Start-Sleep -Milliseconds 400
if (Test-Path -LiteralPath $RestartExe) {
    Start-Process -FilePath $RestartExe -WorkingDirectory $TargetDir
}

try {
    $parent = Split-Path -Parent $SourceDir
    if ($parent -and $parent.StartsWith($env:TEMP, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $SourceDir -Recurse -Force -ErrorAction SilentlyContinue
    }
} catch { }

exit 0
