# =============================================================================
# E6 Car Spa — one-time "trust the publisher" step for a client PC.
#
# Run this ONCE on each PC, BEFORE running E6CarSpa-Setup.exe:
#     Right-click this file  ->  "Run with PowerShell"
# It will ask for administrator permission (click Yes), then tell Windows to trust
# the "Trovotech Solutions" code-signing certificate — so the installer (and app)
# show a VERIFIED PUBLISHER with no security warning, like any certified app.
#
# The .cer file next to this script is the PUBLIC certificate only (no private
# key), so it is safe to carry on a USB stick / email.
# =============================================================================
$ErrorActionPreference = 'Stop'

# --- Self-elevate: "Run with PowerShell" starts WITHOUT admin rights, but importing a
#     machine-wide trusted certificate needs them. If we're not elevated, relaunch this
#     same script elevated (this triggers the Windows "Yes/No" admin prompt) and stop. ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    try {
        Start-Process -FilePath 'powershell.exe' -Verb RunAs `
            -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    } catch {
        Write-Host "Administrator permission was declined. Nothing was changed." -ForegroundColor Yellow
        Read-Host "Press Enter to close"
    }
    return
}

# --- From here on we are running elevated. ---
$cer = Join-Path $PSScriptRoot 'E6CarSpa-Publisher.cer'
if (-not (Test-Path $cer)) {
    Write-Host "ERROR: 'E6CarSpa-Publisher.cer' was not found next to this script." -ForegroundColor Red
    Write-Host "Keep both files together in the same folder and try again." -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}

try {
    # Trusted Root      -> the installer's signature chain validates ("Verified publisher").
    # Trusted Publisher -> Trovotech Solutions is recognised as a software publisher.
    Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root            | Out-Null
    Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null

    Write-Host ""
    Write-Host "  SUCCESS - 'Trovotech Solutions' is now a trusted publisher on this PC." -ForegroundColor Green
    Write-Host "  You can now run E6CarSpa-Setup.exe with no warning." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Tip: copy the installer via USB (not a browser download) so Windows does not" -ForegroundColor DarkGray
    Write-Host "  tag it 'from the internet'. If you did download it, right-click" -ForegroundColor DarkGray
    Write-Host "  E6CarSpa-Setup.exe -> Properties -> tick 'Unblock' -> OK before installing." -ForegroundColor DarkGray
} catch {
    Write-Host ""
    Write-Host "ERROR: could not import the certificate:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to close"
