<#
.SYNOPSIS
  Publishes the API for Linux (self-contained, single file) and packs it into a tarball
  ready to upload to the VPS: dist\e6carspa-api-linux.tar.gz
.DESCRIPTION
  Run this on the dev PC. Upload the tarball plus the deploy/linux folder to the VPS, then
  run setup-vps.sh there (first time) or update-vps.sh logic in VPS-SETUP.md (upgrades).
.EXAMPLE
  ./deploy/linux/publish-linux.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent   # repo root (deploy\linux is two levels down)
Push-Location $root
try {
    Write-Host "== Publishing API for linux-x64 ==" -ForegroundColor Cyan
    dotnet publish src/E6CarSpa.Api -c Release -r linux-x64 --self-contained true `
        -p:PublishSingleFile=true -o dist/api-linux
    if ($LASTEXITCODE) { throw 'API publish failed.' }
    Remove-Item dist/api-linux/*.pdb -ErrorAction SilentlyContinue

    # The server keeps its own appsettings.json (created by setup-vps.sh); shipping the dev one
    # would overwrite the VPS config on upgrade. The template travels separately in deploy/linux.
    Remove-Item dist/api-linux/appsettings.Development.json -ErrorAction SilentlyContinue
    Remove-Item dist/api-linux/appsettings.Local.json -ErrorAction SilentlyContinue

    Write-Host "== Packing tarball ==" -ForegroundColor Cyan
    $tarball = "dist/e6carspa-api-linux.tar.gz"
    Remove-Item $tarball -ErrorAction SilentlyContinue
    tar -czf $tarball -C dist/api-linux .
    if ($LASTEXITCODE) { throw 'tar failed.' }

    $out = Get-Item $tarball
    Write-Host ("== DONE -> {0} ({1:N1} MB) ==" -f $out.FullName, ($out.Length / 1MB)) -ForegroundColor Green
    Write-Host "Next: see deploy/linux/VPS-SETUP.md for upload + install steps." -ForegroundColor Yellow
}
finally { Pop-Location }
