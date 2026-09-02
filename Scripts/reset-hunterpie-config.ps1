# Reset Rise Overlay / HunterPie local config (stored next to HunterPie.exe)
$ErrorActionPreference = "Stop"
Get-Process -Name HunterPie -ErrorAction SilentlyContinue | Stop-Process -Force
$roots = @(
  Join-Path $PSScriptRoot "publish\RiseOverlay",
  Join-Path $PSScriptRoot "HunterPie\bin\Debug\net10.0-windows7.0",
  Join-Path $PSScriptRoot "HunterPie\bin\Release\net10.0-windows7.0"
) | Where-Object { Test-Path $_ }

$files = @("config.json","config.json.bak","internal\account_config.json","internal\feature-flags.json")
foreach ($root in $roots) {
  foreach ($f in $files) {
    $p = Join-Path $root $f
    if (Test-Path $p) {
      Remove-Item -Force $p
      Write-Host "Removed $p"
    }
  }
}
Write-Host "Done. Next launch will use fresh Rise-only defaults."
