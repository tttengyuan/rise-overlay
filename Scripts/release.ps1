# Build Release zip and publish a GitHub Release for Rise Overlay.
# Prerequisites: gh auth login; repo tttengyuan/rise-overlay exists.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Scripts\release.ps1
#   powershell -ExecutionPolicy Bypass -File Scripts\release.ps1 -Version 1.0.1
#   powershell -ExecutionPolicy Bypass -File Scripts\release.ps1 -Version 1.0.1 -SkipPush

param(
    [string]$Version = "",
    [string]$Repo = "tttengyuan/rise-overlay",
    [switch]$SkipPush,
    [switch]$SkipBump
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gh = Join-Path ${env:ProgramFiles} 'GitHub CLI\gh.exe'
if (-not (Test-Path $gh)) { $gh = 'gh' }

$assemblyInfo = Join-Path $root 'HunterPie\Properties\AssemblyInfo.cs'
if (-not $Version) {
    $text = Get-Content $assemblyInfo -Raw
    if ($text -match 'AssemblyInformationalVersion\("([^"]+)"\)') {
        $cur = $Matches[1]
        $parts = $cur.Split('.')
        while ($parts.Count -lt 3) { $parts += '0' }
        $parts[2] = [string]([int]$parts[2] + 1)
        $Version = ($parts[0..2] -join '.')
    } else {
        $Version = '1.0.1'
    }
}

Write-Host "Release version: $Version"

if (-not $SkipBump) {
    $info = Get-Content $assemblyInfo -Raw
    $info = [regex]::Replace($info, 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$Version.0`")")
    $info = [regex]::Replace($info, 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$Version.0`")")
    $info = [regex]::Replace($info, 'AssemblyInformationalVersion\("[^"]+"\)', "AssemblyInformationalVersion(`"$Version`")")
    Set-Content -Path $assemblyInfo -Value $info -NoNewline
    Write-Host "Bumped AssemblyInfo → $Version"
}

Push-Location $root
try {
    dotnet build HunterPie\HunterPie.csproj -c Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sync-publish.ps1')
} finally {
    Pop-Location
}

$pub = Join-Path $root 'publish\RiseOverlay'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zipName = "RiseOverlay-$Version.zip"
$zipPath = Join-Path $dist $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($pub, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host "Packed: $zipPath"

if ($SkipPush) {
    Write-Host "SkipPush set — upload manually:"
    Write-Host "  & `"$gh`" release create `"v$Version`" `"$zipPath`" --repo $Repo --title `"Rise Overlay v$Version`" --generate-notes"
    exit 0
}

# Use ASCII title — PowerShell console encoding often mangles Chinese for gh.exe.
& $gh release create "v$Version" "$zipPath" --repo $Repo --title "Rise Overlay v$Version" --generate-notes
if ($LASTEXITCODE -ne 0) {
    Write-Host "gh release create failed."
    exit 1
}

Write-Host "Done: https://github.com/$Repo/releases/tag/v$Version"
