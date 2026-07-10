# =============================================================================
# E6 Car Spa — one-time "trust the publisher" step for a client PC.
#
# Run this ONCE on each PC, as Administrator, BEFORE running E6CarSpa-Setup.exe.
# It tells Windows to trust the "Trovotech Solutions" code-signing certificate, so
# the installer (and the app) show a VERIFIED PUBLISHER with no security warning —
# the same experience as any certified application, on this PC.
#
# How to run:
#   Right-click this file  ->  "Run with PowerShell"  (approve the admin prompt)
#   — or, in an elevated PowerShell:  .\trust-publisher.ps1
#
# The .cer file next to this script is the PUBLIC certificate only (no private key),
# so it is safe to carry on a USB stick / email.
# =============================================================================
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$cer = Join-Path $PSScriptRoot 'E6CarSpa-Publisher.cer'
if (-not (Test-Path $cer)) {
    Write-Host "ERROR: E6CarSpa-Publisher.cer not found next to this script." -ForegroundColor Red
    exit 1
}

# Trusted Root  -> makes the installer's signature chain validate ("Verified publisher").
# Trusted Publisher -> marks Trovotech Solutions as a recognised software publisher.
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root          | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

Write-Host ""
Write-Host "  Trovotech Solutions is now a trusted publisher on this PC." -ForegroundColor Green
Write-Host "  You can now run E6CarSpa-Setup.exe with no warning." -ForegroundColor Green
Write-Host ""
Write-Host "  Tip: copy the installer via USB (not a browser download) so Windows" -ForegroundColor DarkGray
Write-Host "  doesn't tag it as 'from the internet'. If you did download it, right-click" -ForegroundColor DarkGray
Write-Host "  E6CarSpa-Setup.exe -> Properties -> tick 'Unblock' -> OK before installing." -ForegroundColor DarkGray
