<#
.SYNOPSIS
  One-command release build: publishes the API + desktop (self-contained) and compiles the
  Inno Setup installer. Run from the repo root or anywhere — paths are resolved from the script.
.DESCRIPTION
  Removes the manual publish-both-then-recompile dance. After it finishes, install/ship
  deploy\Output\E6CarSpa-Setup.exe. NOTE: editing source never updates the installed app —
  you must reinstall (or copy the new exe) on the target PC.
.EXAMPLE
  ./deploy/build-release.ps1
#>
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent           # repo root (deploy\ is one level down)
Push-Location $root
try {
    Write-Host "== Stopping repo-local dev instances (they lock the DLLs) ==" -ForegroundColor Cyan
    # Only stop instances running from this repo (dist\ or bin\) — never the installed Windows
    # service in Program Files (it runs from elsewhere and doesn't lock the build output).
    Get-Process E6CarSpa.Desktop, E6CarSpa.Api -ErrorAction SilentlyContinue | ForEach-Object {
        try { if ($_.MainModule.FileName -like "$root*") { $_ | Stop-Process -Force -ErrorAction Stop } } catch { }
    }
    Start-Sleep -Milliseconds 600

    Write-Host "== Publishing API ==" -ForegroundColor Cyan
    dotnet publish src/E6CarSpa.Api -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -o dist/api
    if ($LASTEXITCODE) { throw 'API publish failed.' }
    Remove-Item dist/api/*.pdb -ErrorAction SilentlyContinue

    Write-Host "== Publishing Desktop ==" -ForegroundColor Cyan
    dotnet publish src/E6CarSpa.Desktop -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -o dist/desktop
    if ($LASTEXITCODE) { throw 'Desktop publish failed.' }
    Remove-Item dist/desktop/*.pdb -ErrorAction SilentlyContinue

    Write-Host "== Locating Inno Setup compiler ==" -ForegroundColor Cyan
    $isccCandidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe"
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'ISCC.exe (Inno Setup) not found. Install Inno Setup 6: winget install JRSoftware.InnoSetup' }

    Write-Host "== Compiling installer ($iscc) ==" -ForegroundColor Cyan
    & $iscc /Q "deploy\E6CarSpa.iss"
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }

    $out = Get-Item "deploy\Output\E6CarSpa-Setup.exe"
    Write-Host ("== DONE -> {0} ({1:N1} MB) ==" -f $out.FullName, ($out.Length / 1MB)) -ForegroundColor Green
    Write-Host "Reinstall this on the shop PC to apply the changes." -ForegroundColor Yellow
}
finally { Pop-Location }
