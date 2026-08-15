<#
.SYNOPSIS
  One-command release build: publishes the API + desktop (self-contained) and compiles the
  Inno Setup installer. Run from the repo root or anywhere - paths are resolved from the script.
.DESCRIPTION
  Removes the manual publish-both-then-recompile dance. After it finishes, install/ship
  deploy\Output\E6CarSpa-Setup.exe. NOTE: editing source never updates the installed app -
  you must reinstall (or copy the new exe) on the target PC.
.EXAMPLE
  ./deploy/build-release.ps1
#>
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    Write-Host "== Stopping repo-local dev instances (they lock the DLLs) ==" -ForegroundColor Cyan
    # Stop the API and desktop processes, plus any .NET Host children running from this repo.
    # The latter are the actual file-lock culprits when the API was launched from dist/api\.
    Get-Process E6CarSpa.Desktop, E6CarSpa.Api, ".NET Host" -ErrorAction SilentlyContinue | Where-Object {
        try { $_.MainModule.FileName -like "$root*" } catch { $false }
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

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

    Write-Host "== Compiling installer ==" -ForegroundColor Cyan
    & $iscc /Q "deploy\E6CarSpa.iss"
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }

    $out = Get-Item "deploy\Output\E6CarSpa-Setup.exe"

    Write-Host "== Signing installer ==" -ForegroundColor Cyan
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -match 'CN=Trovotech Solutions' -and $_.HasPrivateKey } |
        Select-Object -First 1
    if ($cert) {
        Set-AuthenticodeSignature -FilePath $out.FullName -Certificate $cert `
            -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com' | Out-Null
        Write-Host "Signed as 'Trovotech Solutions' (run trust-publisher.ps1 on each client PC)." -ForegroundColor Green
    } else {
        Write-Host "WARNING: Trovotech signing cert not found - installer left UNSIGNED." -ForegroundColor Yellow
    }

    $mb = [math]::Round($out.Length / 1MB, 1)
    Write-Host "== DONE -> $($out.FullName) ($mb MB) ==" -ForegroundColor Green
    Write-Host "Reinstall this on the shop PC to apply the changes." -ForegroundColor Yellow
}
finally { Pop-Location }
